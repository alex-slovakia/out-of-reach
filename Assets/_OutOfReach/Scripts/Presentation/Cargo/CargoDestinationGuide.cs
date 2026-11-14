using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace OutOfReach.Presentation.Cargo
{
    /// <summary>A read-only waypoint for the actual bay assigned to the carried equipment.</summary>
    [DisallowMultipleComponent]
    public sealed class CargoDestinationGuide : MonoBehaviour
    {
        [SerializeField] private CargoVisualPresenter cargo;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private UIDocument document;
        [SerializeField] private StyleSheet styleSheet;
        [SerializeField] private Font font;
        private VisualElement host;
        private VisualElement marker;
        private VisualElement chevron;
        private Label caption;

        public void Configure(CargoVisualPresenter source, Camera camera, UIDocument ui,
            StyleSheet styles, Font bodyFont)
        {
            Release();
            cargo = source;
            gameplayCamera = camera;
            document = ui;
            styleSheet = styles;
            font = bodyFont;
        }

        private void OnDisable() => Release();
        private void OnDestroy() => Release();

        private void LateUpdate()
        {
            if (!document || !gameplayCamera || !cargo || !cargo.TryGetCarriedDestination(out Vector3 target))
            {
                if (marker != null) marker.style.display = DisplayStyle.None;
                return;
            }
            VisualElement currentHost = document.rootVisualElement;
            if (currentHost == null || currentHost.panel == null) return;
            if (host != currentHost || marker == null || marker.parent == null) Create(currentHost);
            float width = host.contentRect.width, height = host.contentRect.height;
            if (width < 300f || height < 240f) { marker.style.display = DisplayStyle.None; return; }

            Vector3 projected = gameplayCamera.WorldToViewportPoint(target);
            Vector2 direction = new Vector2(projected.x - .5f, .5f - projected.y);
            if (projected.z <= 0f) direction = -direction;
            if (direction.sqrMagnitude < .0001f) direction = Vector2.right;
            Vector2 centre = new Vector2(width * .5f, height * .5f);
            Vector2 point = new Vector2(projected.x * width, (1f - projected.y) * height - 40f);
            float marginX = 125f, marginY = 90f;
            bool offscreen = projected.z <= 0f || point.x < marginX || point.x > width - marginX
                || point.y < marginY || point.y > height - marginY;
            if (offscreen)
            {
                Vector2 ray = new Vector2(direction.x * width, direction.y * height);
                float scale = Mathf.Min((centre.x - marginX) / Mathf.Max(Mathf.Abs(ray.x), .001f),
                    (centre.y - marginY) / Mathf.Max(Mathf.Abs(ray.y), .001f));
                point = centre + ray * scale;
            }
            marker.style.display = DisplayStyle.Flex;
            marker.style.left = point.x - 110f;
            marker.style.top = point.y;
            chevron.style.rotate = new Rotate(new Angle(offscreen
                ? Mathf.Atan2(direction.y * height, direction.x * width) * Mathf.Rad2Deg - 45f : 45f));
            float metres = Vector3.Distance(gameplayCamera.transform.position, target);
            caption.text = string.Format(CultureInfo.InvariantCulture, "{0} · {1:0.0} m",
                offscreen ? "Cargo bay" : "Stow cargo here", metres);
        }

        private void Create(VisualElement parent)
        {
            Release();
            host = parent;
            marker = new VisualElement { name = "carried-cargo-destination", pickingMode = PickingMode.Ignore };
            marker.AddToClassList("cargo-destination");
            if (styleSheet) marker.styleSheets.Add(styleSheet);
            if (font) marker.style.unityFont = font;
            chevron = new VisualElement { pickingMode = PickingMode.Ignore };
            chevron.AddToClassList("cargo-destination-arrow");
            caption = new Label { pickingMode = PickingMode.Ignore };
            caption.AddToClassList("cargo-destination-caption");
            marker.Add(chevron);
            marker.Add(caption);
            host.Add(marker);
        }

        private void Release()
        {
            marker?.RemoveFromHierarchy();
            host = marker = chevron = null;
            caption = null;
        }
    }
}
