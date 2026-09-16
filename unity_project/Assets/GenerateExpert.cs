using System;
using System.IO;
using System.Net.Sockets;
using UnityEngine;

public class GenerateExpert : MonoBehaviour
{
    public string serverIP = "127.0.0.1"; // Python 서버의 IP 주소
    public int serverPort = 5000;        // Python 서버의 포트 번호
    public string subjectName = "Sample";    // Base name of the saved CSV file
    public int recordingSession = 0;      // Session counter to append to the file name

    [SerializeField] private UserDataRecorder UserDataRecorder;



    public void SendCSVFileInChunks()
    {
        if (UserDataRecorder == null)
        {
            Debug.LogError("UserDataRecorder is not assigned");
            return;
        }

        RequestMatchedExpert(
            UserDataRecorder.GetSubjectName(),
            UserDataRecorder.GetRecordingSession() - 1);
    }

    /// <summary>
    /// Sends one recorded swing to the stroke-specific AutoEncoder service.
    /// The server writes the one selected full expert sequence into the
    /// replay folder before this method returns.
    /// </summary>
    public bool RequestMatchedExpert(string requestedSubject, int requestedSession)
    {
        subjectName = requestedSubject;
        recordingSession = requestedSession;
        string filePath = Path.Combine(
            Application.dataPath, "RecordedData", subjectName,
            $"{subjectName}_Session{recordingSession}.csv");

        if (!File.Exists(filePath))
        {
            Debug.LogError("CSV file not found: " + filePath);
            return false;
        }

        try
        {
            using (TcpClient client = new TcpClient())
            {
                client.SendTimeout = 30000;
                client.ReceiveTimeout = 30000;
                client.Connect(serverIP, serverPort);
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] fileData = File.ReadAllBytes(filePath);
                    int chunkSize = 1024; // 청크 크기 (1KB)
                    int totalChunks = (int)Math.Ceiling((double)fileData.Length / chunkSize);

                    Debug.Log($"Sending CSV file in {totalChunks} chunks: {filePath} (Total Size: {fileData.Length} bytes)");

                    // 전송 시작 메시지에 subjectName과 recordingSession 포함
                    string startMessage = $"START|{subjectName}|{recordingSession}|{totalChunks}\n";
                    byte[] startMessageBytes = System.Text.Encoding.UTF8.GetBytes(startMessage);
                    stream.Write(startMessageBytes, 0, startMessageBytes.Length);

                    // 데이터 분할 전송
                    for (int i = 0; i < totalChunks; i++)
                    {
                        int offset = i * chunkSize;
                        int size = Math.Min(chunkSize, fileData.Length - offset);
                        byte[] chunk = new byte[size];
                        Array.Copy(fileData, offset, chunk, 0, size);

                        stream.Write(chunk, 0, chunk.Length);
                    }

                    // 전송 완료 메시지
                    string endMessage = "END\n";
                    byte[] endMessageBytes = System.Text.Encoding.UTF8.GetBytes(endMessage);
                    stream.Write(endMessageBytes, 0, endMessageBytes.Length);

                    Debug.Log("Swing sent to the AutoEncoder matcher.");

                    // 서버의 응답 받기
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        string response = reader.ReadToEnd().Trim();
                        if (string.IsNullOrEmpty(response) || response.Contains("\"error\""))
                        {
                            Debug.LogError("AutoEncoder matching failed: " + response);
                            return false;
                        }

                        Debug.Log("AutoEncoder match: " + response);
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Could not contact the AutoEncoder matcher at " +
                serverIP + ":" + serverPort + ". Start the stroke-specific matcher first. " + ex.Message);
            return false;
        }
    }
}
