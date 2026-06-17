using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace EscapeBlock9.ProcGen.Navigation
{
    [Serializable]
    public struct RuntimeNavLinkRequest
    {
        public Vector3 Start;
        public Vector3 End;
        public float Width;
        public bool Bidirectional;
        public string Reason;
    }

    public sealed class RuntimeNavMeshBuildReport
    {
        public RuntimeNavMeshBuildReport(IReadOnlyList<string> errors, int sourceCount, int linkCount)
        {
            Errors = errors;
            SourceCount = sourceCount;
            LinkCount = linkCount;
        }

        public IReadOnlyList<string> Errors { get; }
        public int SourceCount { get; }
        public int LinkCount { get; }
    }

    [DisallowMultipleComponent]
    public sealed class RuntimeNavMeshBuilder : MonoBehaviour
    {
        [SerializeField] private LayerMask sourceLayers = ~0;
        [SerializeField] private bool includeColliders = true;
        [SerializeField] private float boundsPadding = 4f;
        [SerializeField] private int navArea = 0;

        private NavMeshData navMeshData;
        private NavMeshDataInstance navMeshDataInstance;
        private readonly List<NavMeshLinkInstance> activeLinks = new List<NavMeshLinkInstance>();

        public IEnumerator RebuildAsync(Transform geometryRoot, IReadOnlyList<RuntimeNavLinkRequest> links, Action<RuntimeNavMeshBuildReport> onCompleted)
        {
            var errors = new List<string>();
            if (geometryRoot == null)
            {
                errors.Add("Nav build failed: geometry root is null.");
                onCompleted?.Invoke(new RuntimeNavMeshBuildReport(errors, 0, 0));
                yield break;
            }

            var sources = new List<NavMeshBuildSource>();
            var markups = new List<NavMeshBuildMarkup>();
            Bounds bounds = CalculateBounds(geometryRoot);
            NavMeshCollectGeometry collectGeometry = includeColliders
                ? NavMeshCollectGeometry.PhysicsColliders
                : NavMeshCollectGeometry.RenderMeshes;
            NavMeshBuilder.CollectSources(bounds, sourceLayers, collectGeometry, navArea, markups, sources);

            if (sources.Count == 0)
            {
                errors.Add("Nav build skipped: no navmesh sources collected.");
                onCompleted?.Invoke(new RuntimeNavMeshBuildReport(errors, 0, 0));
                yield break;
            }

            if (NavMesh.GetSettingsCount() <= 0)
            {
                errors.Add("Nav build failed: no NavMesh agent settings are configured.");
                onCompleted?.Invoke(new RuntimeNavMeshBuildReport(errors, sources.Count, 0));
                yield break;
            }

            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);

            if (navMeshDataInstance.valid)
            {
                navMeshDataInstance.Remove();
            }

            ClearLinks();
            navMeshData ??= new NavMeshData();
            AsyncOperation operation = NavMeshBuilder.UpdateNavMeshDataAsync(navMeshData, settings, sources, bounds);
            while (!operation.isDone)
            {
                yield return null;
            }

            navMeshDataInstance = NavMesh.AddNavMeshData(navMeshData);
            int linkCount = InstallLinks(links, errors);
            onCompleted?.Invoke(new RuntimeNavMeshBuildReport(errors, sources.Count, linkCount));
        }

        private int InstallLinks(IReadOnlyList<RuntimeNavLinkRequest> links, ICollection<string> errors)
        {
            if (links == null || links.Count == 0)
            {
                return 0;
            }

            int installed = 0;
            for (int i = 0; i < links.Count; i++)
            {
                RuntimeNavLinkRequest request = links[i];
                if (request.Start == request.End)
                {
                    continue;
                }

                var linkData = new NavMeshLinkData
                {
                    startPosition = request.Start,
                    endPosition = request.End,
                    width = Mathf.Max(0.1f, request.Width),
                    bidirectional = request.Bidirectional,
                    area = navArea
                };

                NavMeshLinkInstance instance = NavMesh.AddLink(linkData);
                if (!NavMesh.IsLinkValid(instance))
                {
                    errors.Add($"Failed to add nav link ({request.Reason}) from {request.Start} to {request.End}.");
                    continue;
                }

                activeLinks.Add(instance);
                installed++;
            }

            return installed;
        }

        private void ClearLinks()
        {
            for (int i = 0; i < activeLinks.Count; i++)
            {
                if (NavMesh.IsLinkValid(activeLinks[i]))
                {
                    NavMesh.RemoveLink(activeLinks[i]);
                }
            }

            activeLinks.Clear();
        }

        private Bounds CalculateBounds(Transform root)
        {
            bool hasAny = false;
            Bounds bounds = new Bounds(root.position, Vector3.one);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!hasAny)
                {
                    bounds = renderers[i].bounds;
                    hasAny = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            if (!hasAny)
            {
                Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (!hasAny)
                    {
                        bounds = colliders[i].bounds;
                        hasAny = true;
                    }
                    else
                    {
                        bounds.Encapsulate(colliders[i].bounds);
                    }
                }
            }

            bounds.Expand(Mathf.Max(0f, boundsPadding));
            return bounds;
        }
    }
}
