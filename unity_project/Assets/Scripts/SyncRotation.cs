using UnityEngine;

public class SyncRotation : MonoBehaviour
{
    public Transform rightHand; // mixamorig5:RightHand
    public Transform muscleSet; // Muscle Set

    private float initialRightHandY;
    private float initialMuscleSetY;

    void Start()
    {
        if (rightHand == null || muscleSet == null)
        {
            Debug.LogError("Please assign both RightHand and MuscleSet transforms.");
            return;
        }

        // �ʱ� ���� Y�� ȸ���� ����
        initialRightHandY = rightHand.localEulerAngles.y;
        initialMuscleSetY = muscleSet.localEulerAngles.y;
    }

    void Update()
    {
        if (rightHand == null || muscleSet == null) return;

        // RightHand�� ���� Y�� ȸ�� ��ȭ�� ���
        float deltaY = rightHand.localEulerAngles.y - initialRightHandY;

        // Muscle Set�� ���� Y�� ȸ���� ����ȭ
        muscleSet.localEulerAngles = new Vector3(
            muscleSet.localEulerAngles.x,
            initialMuscleSetY + deltaY,
            muscleSet.localEulerAngles.z
        );
    }
}
