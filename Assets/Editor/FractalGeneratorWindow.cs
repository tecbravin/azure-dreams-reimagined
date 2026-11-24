using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AzureDreamsReimagined.Editor
{
    public sealed class FractalGeneratorWindow : EditorWindow
    {
        private enum Fractal { Mandelbrot, Julia }

        private Fractal _fractal;
        private int _resolution = 512;
        private int _iterations = 96;
        private double _centerX = -0.5;
        private double _centerY;
        private double _width = 3.4;
        private double _juliaX = -0.8;
        private double _juliaY = 0.156;
        private Texture2D _preview;

        [MenuItem("Tools/Azure Dreams/Fractal Generator")]
        private static void Open() => GetWindow<FractalGeneratorWindow>("Fractal Generator");

        private void OnGUI()
        {
            var selected = (Fractal)EditorGUILayout.EnumPopup("Tipo", _fractal);
            if (selected != _fractal)
            {
                _fractal = selected;
                _centerX = selected == Fractal.Mandelbrot ? -0.5 : 0;
                _centerY = 0;
                _width = 3.4;
            }
            _resolution = EditorGUILayout.IntSlider("Resolução", _resolution, 128, 1024);
            _iterations = EditorGUILayout.IntSlider("Iterações", _iterations, 16, 256);
            _centerX = EditorGUILayout.DoubleField("Centro X", _centerX);
            _centerY = EditorGUILayout.DoubleField("Centro Y", _centerY);
            _width = EditorGUILayout.DoubleField("Largura visível", _width);
            if (_fractal == Fractal.Julia)
            {
                _juliaX = EditorGUILayout.DoubleField("Constante real", _juliaX);
                _juliaY = EditorGUILayout.DoubleField("Constante imaginária", _juliaY);
            }

            bool valid = Finite(_centerX) && Finite(_centerY) && Finite(_width) && _width > 0 &&
                (_fractal != Fractal.Julia || (Finite(_juliaX) && Finite(_juliaY)));
            using (new EditorGUI.DisabledScope(!valid))
                if (GUILayout.Button("Gerar")) Generate();

            if (!_preview) return;
            if (GUILayout.Button("Exportar PNG")) Export();
            Rect area = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (area.width > 0f && area.height > 0f)
                GUI.DrawTexture(area, _preview, ScaleMode.ScaleToFit);
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private void Generate()
        {
            int size = _resolution;
            var pixels = new Color32[size * size];
            bool julia = _fractal == Fractal.Julia;
            var palette = new Color32[_iterations + 1];
            for (int i = 0; i < _iterations; i++)
                palette[i] = Color.HSVToRGB((0.58f + i * 0.022f) % 1f, 0.78f, 0.94f);
            palette[_iterations] = new Color32(12, 10, 24, 255);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                double px = _centerX + ((x + 0.5) / size - 0.5) * _width;
                double py = _centerY + ((y + 0.5) / size - 0.5) * _width;
                double zx = julia ? px : 0, zy = julia ? py : 0;
                double cx = julia ? _juliaX : px, cy = julia ? _juliaY : py;
                int step = 0;
                while (step < _iterations && zx * zx + zy * zy <= 4)
                {
                    double nextX = zx * zx - zy * zy + cx;
                    zy = 2 * zx * zy + cy;
                    zx = nextX;
                    step++;
                }
                pixels[y * size + x] = palette[step];
            }

            if (_preview) DestroyImmediate(_preview);
            _preview = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _preview.SetPixels32(pixels);
            _preview.Apply();
        }

        private void Export()
        {
            string path = EditorUtility.SaveFilePanel("Exportar fractal", "", "fractal", "png");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                File.WriteAllBytes(path, _preview.EncodeToPNG());
                AssetDatabase.Refresh();
            }
            catch (IOException ex)
            {
                EditorUtility.DisplayDialog("Falha ao exportar", ex.Message, "OK");
            }
            catch (UnauthorizedAccessException ex)
            {
                EditorUtility.DisplayDialog("Falha ao exportar", ex.Message, "OK");
            }
        }

        private void OnDisable()
        {
            if (_preview) DestroyImmediate(_preview);
        }
    }
}
