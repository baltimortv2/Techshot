using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ScreenshotCameraCapture
{
    [MenuItem("TechShot/Screenshot/Capture ScreenshotCamera")]
    public static void CaptureScreenshotCamera()
    {
        var camGo = GameObject.Find("ScreenshotCamera");
        if (camGo == null)
        {
            Debug.LogError("ScreenshotCameraCapture: GameObject 'ScreenshotCamera' not found in the scene.");
            return;
        }

        var cam = camGo.GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogError("ScreenshotCameraCapture: 'ScreenshotCamera' has no Camera component.");
            return;
        }

        int width = Mathf.Max(1, cam.pixelWidth);
        int height = Mathf.Max(1, cam.pixelHeight);

        var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;

        try
        {
            cam.targetTexture = rt;
            RenderTexture.active = rt;
            cam.Render();

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply(false, false);

            var bytes = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            var folder = "Assets/Screenshots";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"rts_{ts}.png";
            string path = Path.Combine(folder, fileName);
            File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            Debug.Log($"ScreenshotCameraCapture: Saved {path}");
        }
        finally
        {
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
        }
    }
}
