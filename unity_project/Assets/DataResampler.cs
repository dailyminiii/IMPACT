using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class DataResampler : MonoBehaviour
{
    public string subjectName = "BK";
    public int sessionNumber = 1;
    public int targetFPS = 30; // 목표 샘플링 속도 (30Hz)

    private string baseFolderPath;

    void Start()
    {
        baseFolderPath = Application.dataPath + "/RecordedData";
    }

    // ✅ 외부에서 실행 가능하도록 public 메서드로 변경
    public void ResampleAndMergeData()
    {
        string forearmFilePath = Path.Combine(baseFolderPath, $"{subjectName}/{subjectName}_Forearm_Session{sessionNumber}.csv");
        string armFilePath = Path.Combine(baseFolderPath, $"{subjectName}/{subjectName}_Arm_Session{sessionNumber}.csv");
        string jointFilePath = Path.Combine(baseFolderPath, $"{subjectName}/{subjectName}_Joint_Session{sessionNumber}.csv");
        string outputFilePath = Path.Combine(baseFolderPath, $"{subjectName}/{subjectName}_Session{sessionNumber}.csv");

        // 데이터 로드
        List<float[]> forearmData = LoadCSVData(forearmFilePath);
        List<float[]> armData = LoadCSVData(armFilePath);
        List<float[]> jointData = LoadCSVData(jointFilePath);

        // 리샘플링 수행
        List<float[]> resampledForearm = ResampleData(forearmData, targetFPS);
        List<float[]> resampledArm = ResampleData(armData, targetFPS);
        List<float[]> resampledJoint = ResampleData(jointData, targetFPS);

        int minCount = Mathf.Min(resampledForearm.Count, resampledArm.Count, resampledJoint.Count);

        List<string> mergedData = new List<string>();
        for (int i = 0; i < minCount; i++)
        {
            string mergedRow = string.Join(",", resampledForearm[i]) + "," +
                            string.Join(",", resampledArm[i]) + "," +
                            string.Join(",", resampledJoint[i]);
            mergedData.Add(mergedRow);
        }

        // 파일 저장
        File.WriteAllLines(outputFilePath, mergedData);
        Debug.Log($"Merged and resampled data saved to: {outputFilePath}");
    }

    private List<float[]> LoadCSVData(string filePath)
    {
        List<float[]> data = new List<float[]>();

        if (!File.Exists(filePath))
        {
            Debug.LogError($"File not found: {filePath}");
            return data;
        }

        string[] lines = File.ReadAllLines(filePath);
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] values = line.Split(',');

            // ✅ 항상 첫 번째 컬럼 제거
            float[] floatValues = values.Skip(1)
                                        .Select(v => float.TryParse(v, out float result) ? result : 0f)
                                        .ToArray();

            data.Add(floatValues);
        }

        return data;
    }


    private List<float[]> ResampleData(List<float[]> originalData, int targetFPS)
    {
        if (originalData.Count == 0) return new List<float[]>();

        int originalSize = originalData.Count;
        int targetSize = Mathf.RoundToInt((originalSize / 30f) * targetFPS);

        List<float[]> resampledData = new List<float[]>();

        for (int i = 0; i < targetSize; i++)
        {
            float t = (float)i / (targetSize - 1) * (originalSize - 1);
            int index = Mathf.FloorToInt(t);
            int nextIndex = Mathf.Min(index + 1, originalSize - 1);
            float blendFactor = t - index;

            float[] interpolatedRow = new float[originalData[0].Length];
            for (int j = 0; j < interpolatedRow.Length; j++)
            {
                interpolatedRow[j] = Mathf.Lerp(originalData[index][j], originalData[nextIndex][j], blendFactor);
            }

            resampledData.Add(interpolatedRow);
        }

        return resampledData;
    }
}
