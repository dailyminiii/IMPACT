using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DataProvider : MonoBehaviour
{
    public Dictionary<string, double> chartData = new Dictionary<string, double>();

    private void Start()
    {
        // 예제 데이터 (실시간으로 변경 가능)
        chartData["Apple"] = 10;
        chartData["Banana"] = 20;
        chartData["Orange"] = 15;
        chartData["Grapes"] = 25;
    }

    public void UpdateData(string category, double value)
    {
        if (chartData.ContainsKey(category))
            chartData[category] = value;
        else
            chartData.Add(category, value);
    }

    public Dictionary<string, double> GetChartData()
    {
        return chartData;
    }
}
