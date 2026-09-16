using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using MixedReality.Toolkit.UX;
using UnityEngine;

public class scaleController : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private GameObject expertObject;
    [SerializeField] private GameObject playerObject;
    [SerializeField] private GameObject realTimeAvatarObject;
    [SerializeField, Min(0f)] private float additionalSeparationPerScaleStep = 1.0f;
    [SerializeField] private bool createRoleLabels;
    [SerializeField, Min(0f)] private float roleLabelFootOffset = 0.06f;
    [SerializeField, Min(0.001f)] private float roleLabelCharacterSize = 0.014f;

    private float _scaleValue = 1f; // 기본값 설정
    private Vector3 _expertBaseLocalPosition;
    private Vector3 _playerBaseLocalPosition;
    private bool _hasBasePositions;
    private bool _labelsCreated;
    private readonly List<RoleLabel> _roleLabels = new List<RoleLabel>();

    private sealed class RoleLabel {
        public Transform transform;
        public Transform owner;
        public Renderer[] avatarRenderers;
    }

    public float ScaleValue => _scaleValue; // 외부에서 가져갈 수 있도록 프로퍼티 제공

    private void Awake() {
        CaptureBasePositions();
    }

    private void Start() {
        if (slider != null) {
            slider.OnValueUpdated.AddListener(OnSliderValueChanged);
            OnSliderValueChanged(new SliderEventData(slider.Value, slider.Value));
        }

        CreateRoleLabels();
    }

    public void OnSliderValueChanged(SliderEventData eventData) {
        _scaleValue = eventData.NewValue; // 현재 스케일 값 업데이트

        CaptureBasePositions();

        if (expertObject != null) {
            expertObject.transform.localScale = new Vector3(_scaleValue, _scaleValue, _scaleValue);
        }

        if (playerObject != null){
            playerObject.transform.localScale = new Vector3(_scaleValue, _scaleValue, _scaleValue);
        }

        ApplySideBySideLayout();

        Debug.Log($"Scale Changed! New Value: {_scaleValue}");
    }

    private void CaptureBasePositions() {
        if (_hasBasePositions || expertObject == null || playerObject == null) {
            return;
        }

        _expertBaseLocalPosition = expertObject.transform.localPosition;
        _playerBaseLocalPosition = playerObject.transform.localPosition;
        _hasBasePositions = true;
    }

    private void ApplySideBySideLayout() {
        if (!_hasBasePositions || expertObject == null || playerObject == null) {
            return;
        }

        // Preserve the original composition through 1x. Above 1x, move both
        // avatars equally away from their shared midpoint so enlargement does
        // not make the two bodies overlap.
        float scaleExcess = Mathf.Max(0f, _scaleValue - 1f);
        Vector3 originalSeparation = _playerBaseLocalPosition - _expertBaseLocalPosition;
        Vector3 direction = originalSeparation.sqrMagnitude > 0.0001f
            ? originalSeparation.normalized
            : Vector3.right;
        Vector3 midpoint = (_expertBaseLocalPosition + _playerBaseLocalPosition) * 0.5f;
        float separation = originalSeparation.magnitude + additionalSeparationPerScaleStep * scaleExcess;

        expertObject.transform.localPosition = midpoint - direction * (separation * 0.5f);
        playerObject.transform.localPosition = midpoint + direction * (separation * 0.5f);
    }

    private void CreateRoleLabels() {
        if (!createRoleLabels || _labelsCreated) {
            return;
        }

        _labelsCreated = true;
        CreateRoleLabel(expertObject, "Replay Expert", new Color(0.45f, 0.72f, 1f));
        CreateRoleLabel(playerObject, "Replay Avatar", Color.white);
        CreateRoleLabel(realTimeAvatarObject, "Real-Time Avatar", new Color(0.35f, 1f, 0.68f));
    }

    private void CreateRoleLabel(GameObject avatar, string text, Color color) {
        if (avatar == null) {
            Debug.LogWarning($"Cannot create the '{text}' label because its avatar is not assigned.");
            return;
        }

        GameObject labelObject = new GameObject($"{text} Label");
        Transform labelTransform = labelObject.transform;
        labelTransform.SetParent(avatar.transform, false);

        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = text;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 64;
        label.characterSize = roleLabelCharacterSize;
        label.color = color;

        _roleLabels.Add(new RoleLabel {
            transform = labelTransform,
            owner = avatar.transform,
            // The TextMesh created above is also a Renderer. Exclude it from the
            // avatar bounds; otherwise the label would include itself and move
            // farther down every frame.
            avatarRenderers = avatar.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer != null && renderer.gameObject != labelObject)
                .ToArray()
        });
    }

    private void LateUpdate() {
        Camera activeCamera = Camera.main;
        if (activeCamera == null) {
            return;
        }

        foreach (RoleLabel label in _roleLabels) {
            if (label.transform == null || label.owner == null) {
                continue;
            }

            // Anchor each label just beneath the rendered feet, rather than at a
            // fixed height above the avatar. This keeps the role association clear
            // when the replay avatars are scaled or moved side by side.
            if (TryGetAvatarBounds(label.avatarRenderers, out Bounds avatarBounds)) {
                Vector3 labelPosition = avatarBounds.center;
                labelPosition.y = avatarBounds.min.y - roleLabelFootOffset;
                label.transform.position = labelPosition;
            }

            // Counter the owner's scale so labels remain compact and legible.
            float ownerScale = Mathf.Max(0.001f, label.owner.lossyScale.x);
            label.transform.localScale = Vector3.one / ownerScale;
            label.transform.rotation = Quaternion.LookRotation(
                label.transform.position - activeCamera.transform.position,
                activeCamera.transform.up);
        }
    }

    private static bool TryGetAvatarBounds(Renderer[] renderers, out Bounds avatarBounds) {
        avatarBounds = new Bounds();
        bool hasBounds = false;

        if (renderers == null) {
            return false;
        }

        foreach (Renderer renderer in renderers) {
            if (renderer == null || !renderer.gameObject.activeInHierarchy) {
                continue;
            }

            if (!hasBounds) {
                avatarBounds = renderer.bounds;
                hasBounds = true;
            } else {
                avatarBounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private void OnDestroy() {
        if (slider != null) {
            slider.OnValueUpdated.RemoveListener(OnSliderValueChanged);
        }
    }
}
