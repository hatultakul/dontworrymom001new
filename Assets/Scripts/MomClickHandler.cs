using UnityEngine;

namespace DontWorryMom
{
    /// <summary>
    /// Attach to the Mom GameObject.
    /// Left-click: toggle Play/Pause (Time.timeScale).
    /// Works in both Edit-time preview and at runtime.
    /// </summary>
    public class MomClickHandler : MonoBehaviour
    {
        private bool _paused = false;

        private void OnMouseDown()
        {
            _paused = !_paused;
            Time.timeScale = _paused ? 0f : 1f;
            Debug.Log(_paused ? "[Mom] Game paused." : "[Mom] Game resumed.");
        }

        // Visual feedback: tint the sprite red while paused
        private void Update()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.color = _paused ? new Color(1f, 0.4f, 0.4f) : Color.white;
        }
    }
}
