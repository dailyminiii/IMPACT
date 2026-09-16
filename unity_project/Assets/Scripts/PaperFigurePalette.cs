using System.Collections.Generic;
using System.Linq;
using ChartAndGraph;
using UnityEngine;

/// <summary>
/// Applies the semantic colour mapping used in the paper figure to the live and
/// replay EMG charts. Existing chart materials are cloned so their shaders,
/// thicknesses, and rendering options remain unchanged.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class PaperFigurePalette : MonoBehaviour
{
    private static readonly Color Flexion = new Color32(127, 127, 127, 255);
    private static readonly Color Extension = new Color32(9, 129, 74, 255);
    private static readonly Color Expert = new Color32(98, 95, 80, 255);
    private static readonly Color User = new Color32(255, 255, 255, 255);
    private const float UserReplayFillOpacity = 0.18f;

    private readonly List<Material> runtimeMaterials = new List<Material>();

    private void Start()
    {
        ApplyBarPalette();
        ApplyReplayPalette();
    }

    private void ApplyBarPalette()
    {
        foreach (CanvasBarChart chart in FindObjectsOfType<CanvasBarChart>(true))
        {
            ApplyBarCategory(chart, "Flexion", Flexion);
            ApplyBarCategory(chart, "Extension", Extension);
        }
    }

    private void ApplyBarCategory(CanvasBarChart chart, string category, Color colour)
    {
        if (chart == null || !chart.DataSource.HasCategory(category))
        {
            return;
        }

        ChartDynamicMaterial current = chart.DataSource.GetMaterial(category);
        Material material = CreateTintedMaterial(current != null ? current.Normal : null, colour);
        chart.DataSource.SetMaterial(category, new ChartDynamicMaterial(material, colour, colour));
    }

    private void ApplyReplayPalette()
    {
        foreach (GraphChart chart in FindObjectsOfType<GraphChart>(true))
        {
            ApplyGraphCategory(chart, "expert", Expert);
            ApplyGraphCategory(chart, "user", User);
        }
    }

    private void ApplyGraphCategory(GraphChart chart, string category, Color colour)
    {
        if (chart == null || !chart.DataSource.CategoryNames.Contains(category))
        {
            return;
        }

        chart.DataSource.GetCategoryLine(category, out Material line, out double thickness, out MaterialTiling tiling);
        if (line != null)
        {
            // White user traces need slightly more weight to remain legible over
            // the dark replay panel and the expert trace.
            double visibleThickness = category == "user" ? System.Math.Max(thickness, 3.5d) : thickness;
            chart.DataSource.SetCategoryLine(category, CreateTintedMaterial(line, colour), visibleThickness, tiling);
        }

        chart.DataSource.GetCategoryFill(category, out Material fill, out bool stretchFill);
        if (fill != null)
        {
            // Preserve the area encoding for the user while using a translucent
            // white fill so the expert trace remains readable underneath.
            Color fillColour = category == "user"
                ? new Color(colour.r, colour.g, colour.b, UserReplayFillOpacity)
                : colour;
            chart.DataSource.SetCategoryFill(category, CreateTintedMaterial(fill, fillColour), stretchFill);
        }

        chart.DataSource.GetCategoryPoint(category, out Material point, out double pointSize);
        if (point != null)
        {
            double visiblePointSize = category == "user" ? System.Math.Max(pointSize, 2d) : pointSize;
            chart.DataSource.SetCategoryPoint(category, CreateTintedMaterial(point, colour), visiblePointSize);
        }
    }

    private Material CreateTintedMaterial(Material source, Color colour)
    {
        Shader fallbackShader = Shader.Find("UI/Default") ?? Shader.Find("Sprites/Default");
        Material material = source != null ? new Material(source) : new Material(fallbackShader);
        runtimeMaterials.Add(material);

        if (material.HasProperty("_Color"))
        {
            material.color = colour;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", colour);
        }

        return material;
    }

    private void OnDestroy()
    {
        foreach (Material material in runtimeMaterials)
        {
            if (material != null)
            {
                Destroy(material);
            }
        }
    }
}
