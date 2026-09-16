using System.Collections;
using MixedReality.Toolkit.UX;
using UnityEngine;

public class speedController : MonoBehaviour
{
    [SerializeField] private Slider slider; // MRTK 슬라이더
    private float _speedScaler = 3f; // 기본값 설정

    public float SpeedScaler => _speedScaler; // 외부에서 접근 가능하도록 프로퍼티 제공

    private void Start() {
        if (slider != null) {
            slider.OnValueUpdated.AddListener(OnSliderValueChanged);
            OnSliderValueChanged(new SliderEventData(slider.Value, slider.Value));
        }
    }

    public void OnSliderValueChanged(SliderEventData eventData) {
        _speedScaler = Mathf.Clamp(eventData.NewValue, 0.25f, 3.0f); // 최소 0.1배, 최대 2배
        Debug.Log($"Speed Scaler Updated! New Value: {_speedScaler}");
    }

    private void OnDestroy() {
        if (slider != null) {
            slider.OnValueUpdated.RemoveListener(OnSliderValueChanged);
        }
    }
}
