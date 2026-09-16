using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class GenerateMVC : MonoBehaviour
{
    public string subjectName = "Sub00"; // 기본 서브젝트명
    public string dataFolderPath = "./RecordedData/"; // CSV 파일 저장 경로

    private Dictionary<string, float[]> mvcData = new Dictionary<string, float[]>(); // MVC 데이터를 저장하는 Dictionary
    private string[] directions = { "Up", "Down", "Left", "Right" }; // 방향 목록


    void Start()
    {
        // MVC 데이터를 자동으로 로드하고 최대값 계산
        LoadAndProcessMVC("Arm");
        LoadAndProcessMVC("Forearm");
    }

    // MVC 데이터 로드 및 최댓값 계산
    public void LoadAndProcessMVC(string muscleType)
    {
        float[] maxValues = new float[8]; // 8개 채널의 최댓값 저장 (초기화)
        bool foundRecording = false;

        foreach (var direction in directions)
        {
            // 특정 방향에 해당하는 모든 파일 탐색
            string searchPattern = $"{subjectName}/{subjectName}_{muscleType}_{direction}_*.csv";
            string[] matchingFiles = Directory.GetFiles(dataFolderPath, searchPattern);

            if (matchingFiles.Length == 0)
            {
                Debug.LogWarning($"No files found for pattern: {searchPattern}");
                continue;
            }

            foundRecording = true;

            Debug.Log("matchingFiles : " + matchingFiles);

            // 각 파일에 대해 처리
            foreach (var filePath in matchingFiles)
            {
                string[] lines = File.ReadAllLines(filePath);

                for (int i = 1; i < lines.Length; i++) // 첫 줄은 헤더이므로 제외
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;

                    string[] parts = lines[i].Split(',');
                    int channelIndex = int.Parse(parts[0].Replace("Channel ", "").Trim()) - 1;
                    float value = float.Parse(parts[1].Trim());

                    Debug.Log("value : " + value);

                    // 최댓값 갱신
                    if (value > maxValues[channelIndex])
                    {
                        maxValues[channelIndex] = value;
                    }
                }
            }
        }

        // Do not overwrite an existing calibration with eight zeros when no
        // MVC recording exists (as is the case in the public replay sample).
        if (!foundRecording)
        {
            Debug.LogWarning($"No {muscleType} MVC recordings were found; preserving the existing calibration file.");
            return;
        }

        // 최댓값을 저장
        mvcData[muscleType] = maxValues;

        // 최댓값을 CSV로 저장
        SaveMaxMVCToCSV(muscleType, maxValues);
    }

    // MVC 최댓값을 CSV로 저장
    private void SaveMaxMVCToCSV(string muscleType, float[] maxValues)
    {
        string savePath = Path.Combine(dataFolderPath, $"{subjectName}/{subjectName}{muscleType}.csv");

        // 폴더 없으면 생성
        if (!Directory.Exists(dataFolderPath))
        {
            Directory.CreateDirectory(dataFolderPath);
        }

        StringWriter csvContent = new StringWriter();
        csvContent.WriteLine("Channel, MVC Value");

        for (int i = 0; i < maxValues.Length; i++)
        {
            csvContent.WriteLine($"Channel {i + 1},{maxValues[i]}");
        }

        File.WriteAllText(savePath, csvContent.ToString());
    }
}
