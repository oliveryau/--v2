using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public static class UiGraphicFade
{
    public static Color[] CaptureColors(Graphic[] graphics)
    {
        if (graphics == null)
            return System.Array.Empty<Color>();

        Color[] colors = new Color[graphics.Length];

        for (int i = 0; i < graphics.Length; i++)
            colors[i] = graphics[i] != null ? graphics[i].color : Color.white;

        return colors;
    }

    public static Color[] BuildTransparentColors(Color[] targetColors)
    {
        if (targetColors == null)
            return System.Array.Empty<Color>();

        Color[] transparentColors = new Color[targetColors.Length];

        for (int i = 0; i < targetColors.Length; i++)
        {
            transparentColors[i] = targetColors[i];
            transparentColors[i].a = 0f;
        }

        return transparentColors;
    }

    public static void RestoreColors(Graphic[] graphics, Color[] colors)
    {
        if (graphics == null || colors == null)
            return;

        int count = Mathf.Min(graphics.Length, colors.Length);

        for (int i = 0; i < count; i++)
        {
            if (graphics[i] != null)
                graphics[i].color = colors[i];
        }
    }

    public static IEnumerator FadeColors(
        Graphic[] graphics,
        Color[] fromColors,
        Color[] toColors,
        float duration)
    {
        if (graphics == null || graphics.Length == 0)
            yield break;

        if (fromColors == null || toColors == null
            || fromColors.Length != graphics.Length
            || toColors.Length != graphics.Length)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            RestoreColors(graphics, toColors);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null)
                    graphics[i].color = Color.Lerp(fromColors[i], toColors[i], t);
            }

            yield return null;
        }

        RestoreColors(graphics, toColors);
    }

    public static void SetAlpha(Graphic[] graphics, float alpha)
    {
        if (graphics == null)
            return;

        for (int i = 0; i < graphics.Length; i++)
            SetAlpha(graphics[i], alpha);
    }

    public static void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null)
            return;

        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    public static void RestoreAlpha(Graphic[] graphics)
    {
        if (graphics == null)
            return;

        for (int i = 0; i < graphics.Length; i++)
            RestoreAlpha(graphics[i]);
    }

    public static void RestoreAlpha(Graphic graphic)
    {
        if (graphic == null)
            return;

        Color color = graphic.color;
        color.a = 1f;
        graphic.color = color;
    }

    public static IEnumerator FadeAlpha(Graphic[] graphics, float fromAlpha, float toAlpha, float duration)
    {
        if (graphics == null || graphics.Length == 0)
            yield break;

        if (duration <= 0f)
        {
            SetAlpha(graphics, toAlpha);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(graphics, Mathf.Lerp(fromAlpha, toAlpha, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetAlpha(graphics, toAlpha);
    }

    public static IEnumerator FadeToColors(Graphic[] graphics, Color[] targetColors, float duration)
    {
        if (graphics == null || graphics.Length == 0)
            yield break;

        if (targetColors == null || targetColors.Length != graphics.Length)
            yield break;

        Color[] startColors = BuildTransparentColors(targetColors);
        yield return FadeColors(graphics, startColors, targetColors, duration);
    }
}
