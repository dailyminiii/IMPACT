using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ChartAndGraph;

public class CanvasBarChartUpdater : MonoBehaviour
{
    public CanvasBarChart barChart;
    public DataProvider dataProvider; // 데이터 제공 스크립트 연결

    private void Start()
    {
        if (dataProvider == null || barChart == null)
        {
            Debug.LogError("DataProvider 또는 CanvasBarChart가 할당되지 않았습니다.");
            return;
        }

        UpdateChart();
    }

    public void UpdateChart()
    {
        Dictionary<string, double> data = dataProvider.GetChartData();

      
        foreach (var entry in data)
        {
            string category = entry.Key;
            double value = entry.Value;

        }

        barChart.InternalGenerateChart(); // 차트 갱신
    }
}
