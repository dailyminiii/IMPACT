using UnityEngine;

public class MatchTransform : MonoBehaviour
{
    public Transform target1; // 기준이 되는 첫 번째 오브젝트
    public Transform target2; // 위치와 회전을 맞출 두 번째 오브젝트
    public Transform target3; // 위치와 회전을 맞출 세 번째 오브젝트

    public Vector3 positionOffset2 = Vector3.zero; // target2의 위치 오프셋
    public Vector3 positionOffset3 = Vector3.zero; // target3의 위치 오프셋
    public Vector3 rotationOffset2 = Vector3.zero; // target2의 회전 오프셋
    public Vector3 rotationOffset3 = Vector3.zero; // target3의 회전 오프셋

    public void ApplyTransform()
    {
        if (target1 == null || target2 == null || target3 == null)
        {
            Debug.LogWarning("Target이 올바르게 설정되지 않았습니다!");
            return;
        }

        // target1 기준으로 target2 위치 조정
        target2.position = target1.position 
                         + target1.right * positionOffset2.x 
                         + target1.up * positionOffset2.y 
                         + target1.forward * positionOffset2.z;

        // target1 기준으로 target3 위치 조정
        target3.position = target1.position 
                         + target1.right * positionOffset3.x 
                         + target1.up * positionOffset3.y 
                         + target1.forward * positionOffset3.z;

        // 회전 조정 (오프셋 추가)
        target2.rotation = target1.rotation * Quaternion.Euler(rotationOffset2);
        target3.rotation = target1.rotation * Quaternion.Euler(rotationOffset3);
        
        Debug.Log("Transform 적용 완료!");
    }
}
