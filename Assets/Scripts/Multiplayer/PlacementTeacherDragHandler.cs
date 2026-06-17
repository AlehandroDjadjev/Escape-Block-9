using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Attached to each teacher item in the placement list so the Teacher Placer can
// drag a teacher portrait onto the top-down map. Reports drag lifecycle to the
// runtime via plain callbacks — the runtime owns the ghost cursor visual and the
// "did this drop hit the map?" check, so this handler stays dumb.
public sealed class PlacementTeacherDragHandler : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int slotIndex;
    public Action<int, Vector2> onBeginDrag;
    public Action<int, Vector2> onDrag;
    public Action<int, Vector2> onEndDrag;

    public void OnBeginDrag(PointerEventData eventData)
    {
        onBeginDrag?.Invoke(slotIndex, ReadCursor(eventData));
    }

    public void OnDrag(PointerEventData eventData)
    {
        onDrag?.Invoke(slotIndex, ReadCursor(eventData));
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        onEndDrag?.Invoke(slotIndex, ReadCursor(eventData));
    }

    // Use the new Input System's Mouse position when available so we get the same
    // coordinate space the rest of the placement code is using.
    private static Vector2 ReadCursor(PointerEventData eventData)
    {
        if (Mouse.current != null) return Mouse.current.position.ReadValue();
        return eventData.position;
    }
}
