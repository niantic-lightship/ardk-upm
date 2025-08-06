// Copyright 2022-2025 Niantic.

using System;
using System.Collections;
using System.IO;
using Niantic.Lightship.AR.Loader;
using Niantic.Lightship.AR.Subsystems.Playback;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace Niantic.Lightship.AR.Editor
{
    public abstract class BasePlaybackSettingsEditor : IPlaybackSettingsEditor
    {
        private static class Contents
        {
            public static readonly GUIContent UsePlaybackLabel =
                new GUIContent
                (
                    "Enabled",
                    "When enabled, a dataset will be used to simulate live AR input."
                );

            public static readonly GUIContent DatasetPathLabel =
                new GUIContent
                (
                    "Dataset Path",
                    "The absolute path to the folder containing the dataset to use for playback in-Editor. " +
                    "Does not need to be within the Unity project directory."
                );

            public static readonly GUIContent RunManuallyLabel =
                new GUIContent
                (
                    "Run Manually",
                    "Use the space bar to move forward frame by frame."
                );

            public static readonly GUIContent LoopInfinitelyLabel =
                new GUIContent
                (
                    "Loop Infinitely",
                    "When enabled, the playback dataset will play continuously rather than halt at the end. " +
                    "To prevent immediate jumps in pose and tracking, the dataset will alternate running forwards and backwards."
                );
        }

        protected abstract ILightshipPlaybackSettings PlaybackSettings { get; }

        private PlaybackDataset _dataset;
        private Texture2D[] _frameTextures;
        private bool _previewTexturesLoaded;
        private bool _allTexturesLoaded;

        private bool _drawPreviewStartFrame = false;
        private bool _drawPreviewEndFrame = false;
        private bool _onlyLoadPreviews = false;

        private const int PreviewLengthThreshold = 1800;
        private const int PreviewFrameCount = 24;
        private int FrameCount
        {
            get
            {
                if (_frameTextures != null)
                    return _frameTextures.Length;
                else
                    return PreviewFrameCount;
            }
        }

        private float _loadProgress = 0.0f;

        private EditorCoroutine _loadFrameTexturesCoroutine;

        private Texture2D _backgroundTexture;

        private Texture2D _overlayTexture;

        public void DrawGUI()
        {
            var currUsedPlayback = PlaybackSettings.UsePlayback;
            var newUsePlayback = EditorGUILayout.Toggle(Contents.UsePlaybackLabel, currUsedPlayback);
            if (newUsePlayback != currUsedPlayback)
            {
                PlaybackSettings.UsePlayback = newUsePlayback;
            }

            EditorGUI.BeginDisabledGroup(!newUsePlayback);
            {
                var wasPathUpdated = DrawDatasetPathGUI();

                if (string.IsNullOrEmpty(PlaybackSettings.PlaybackDatasetPath))
                {
                    _dataset = null;
                    _previewTexturesLoaded = false;
                    _allTexturesLoaded = false;
                    _onlyLoadPreviews = false;
                    _frameTextures = null;
                }
                else if(_dataset == null || wasPathUpdated)
                {
                    _dataset = PlaybackDatasetLoader.Load(PlaybackSettings.PlaybackDatasetPath);
                    _previewTexturesLoaded = false;
                    _allTexturesLoaded = false;
                    _onlyLoadPreviews = false;
                    _frameTextures = null;
                    if(_loadFrameTexturesCoroutine != null)
                    {
                        EditorCoroutineUtility.StopCoroutine(_loadFrameTexturesCoroutine);
                    }
                    _loadFrameTexturesCoroutine = EditorCoroutineUtility.StartCoroutine(LoadFrameTextures(), this);
                }

                if (_dataset != null)
                {
                    var dummyLayout = EditorGUILayout.GetControlRect(
                        GUILayout.ExpandWidth(true),
                        GUILayout.ExpandHeight(true));
                    DrawTrimToolGUI(wasPathUpdated, dummyLayout);
                }
                else
                {
                    EditorGUILayout.HelpBox("Please enter a valid Playback dataset path", MessageType.Warning);
                }

                var currRunManually = PlaybackSettings.RunManually;
                var changedRunManually = EditorGUILayout.Toggle(Contents.RunManuallyLabel, currRunManually);
                if (changedRunManually != currRunManually)
                {
                    PlaybackSettings.RunManually = changedRunManually;
                }

                var currLoopInfinitely = PlaybackSettings.LoopInfinitely;
                var changedLoopInfinitely = EditorGUILayout.Toggle(Contents.LoopInfinitelyLabel, currLoopInfinitely);
                if (changedLoopInfinitely != currLoopInfinitely)
                {
                    PlaybackSettings.LoopInfinitely = changedLoopInfinitely;
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        private bool DrawDatasetPathGUI()
        {
            EditorGUILayout.BeginHorizontal();

            var currPath = PlaybackSettings.PlaybackDatasetPath;
            var changedPath = EditorGUILayout.TextField(Contents.DatasetPathLabel, currPath);
            var wasPathUpdated = false;

            if (changedPath != currPath)
            {
                PlaybackSettings.PlaybackDatasetPath = changedPath;
                wasPathUpdated = true;
            }

            var browse = GUILayout.Button("Browse", GUILayout.Width(125));

            EditorGUILayout.EndHorizontal();

            if (browse)
            {
                var browsedPath =
                    EditorUtility.OpenFolderPanel
                    (
                        "Select Dataset Directory",
                        changedPath,
                        ""
                    );

                if (!browsedPath.Equals(currPath))
                {
                    if (browsedPath.Length > 0)
                    {
                        PlaybackSettings.PlaybackDatasetPath = browsedPath;
                    }

                    wasPathUpdated = true;
                }
            }

            return wasPathUpdated;
        }

        private void DrawTrimToolGUI(bool wasPathUpdated, Rect window)
        {
            if (_dataset == null)
            {
                return;
            }

            var startFrame = wasPathUpdated ? 0f : PlaybackSettings.StartFrame;
            var maxFrame = _dataset.FrameCount - 1;
            var endFrame = wasPathUpdated ? maxFrame : (float) PlaybackSettings.EndFrame;
            var aspectRatio = (float)_dataset.Resolution.x / (float)_dataset.Resolution.y;

            switch (_dataset.Frames[0].Orientation)
            {
                case ScreenOrientation.Portrait:
                case ScreenOrientation.PortraitUpsideDown:
                    aspectRatio = 1.0f / aspectRatio;
                    break;
                case ScreenOrientation.LandscapeLeft:
                case ScreenOrientation.LandscapeRight:
                case ScreenOrientation.AutoRotation:
                case ScreenOrientation.Unknown:
                default:
                    break;
            }

            if (Mathf.Approximately(endFrame, -1))
            {
                endFrame = maxFrame;
            }

            startFrame = Mathf.RoundToInt(Mathf.Clamp(startFrame, 0, endFrame - 1));
            endFrame = Mathf.RoundToInt(Mathf.Clamp(endFrame, startFrame + 1, maxFrame));

            if (_backgroundTexture == null)
                _backgroundTexture = MakeTex(2, 2, new Color(0.2f, 0.2f, 0.2f, 1.0f));

            if (_overlayTexture == null)
                _overlayTexture = MakeTex(84, 84, new Color(Color.gray.r, Color.gray.g, Color.gray.b, 0.6f));

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            {
                var darkGrayBackground = new GUIStyle(GUI.skin.box)
                {
                    normal =
                    {
                        background = _backgroundTexture
                    }
                };

                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.Space(8, false);
                    EditorGUILayout.BeginVertical();
                    {
                        var imageHeight = 64;
                        var imageWidth = Mathf.RoundToInt(imageHeight * aspectRatio);

                        var framesToSkip = _dataset.FrameCount <= PreviewFrameCount ?
                            1 :
                            _dataset.FrameCount / PreviewFrameCount;

                        var newImageCount = Mathf.Max(_dataset.FrameCount, PreviewFrameCount);

                        float spacing = ((window.width - 16) - (newImageCount * imageWidth)) / (newImageCount - 1);

                        var overlayStyle = new GUIStyle() { stretchHeight = true, stretchWidth = true, normal =
                        {
                            background = _overlayTexture
                        }};

                        // Timeline
                        Rect timeline = DrawTimeline(window, darkGrayBackground, framesToSkip, imageWidth, imageHeight, spacing, aspectRatio);

                        var startOverlayWidth = timeline.width * (startFrame / maxFrame);
                        var endOverlayWidth = timeline.width * ((maxFrame - endFrame) / maxFrame);

                        // Overlays
                        DrawOverlays(timeline, startOverlayWidth, overlayStyle, endOverlayWidth);

                        if(!_allTexturesLoaded)
                        {
                            EditorGUILayout.HelpBox($"Loading playback dataset... ({_loadProgress:F2}%)", MessageType.Info);
                        }

                        // Slider
                        startFrame = DrawSlider(startFrame, maxFrame, timeline, imageWidth, aspectRatio, ref endFrame);

                        // Timestamps
                        DrawTimestamps(startFrame, endFrame);
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(8, false);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(10);
            }
            EditorGUILayout.EndVertical();

            // Apply changes to the settings object
            if (GUI.changed)
            {
                PlaybackSettings.StartFrame = (int)startFrame;
                PlaybackSettings.EndFrame = (int)endFrame;
            }
        }

        private static void DrawOverlays(
            Rect timeline,
            float startOverlayWidth,
            GUIStyle overlayStyle,
            float endOverlayWidth)
        {
            GUI.Box(new Rect(timeline.position, new Vector2(startOverlayWidth, 84)), GUIContent.none, overlayStyle);
            GUI.Box(new Rect(timeline.position + new Vector2(timeline.width - endOverlayWidth, 0), new Vector2(endOverlayWidth, 84)), GUIContent.none, overlayStyle);
        }

        private void DrawTimestamps(float startFrame, float endFrame)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            {
                var startTime = _dataset.Frames[Mathf.RoundToInt(startFrame)].TimestampInSeconds - _dataset.Frames[0].TimestampInSeconds;
                var endTime = _dataset.Frames[Mathf.RoundToInt(endFrame)].TimestampInSeconds - _dataset.Frames[0].TimestampInSeconds;

                GUILayout.Label(TimeSpan.FromSeconds(startTime).ToString(@"mm\:ss\:ff"), GUILayout.Width(60));
                EditorGUILayout.Space();
                GUILayout.Label(TimeSpan.FromSeconds(endTime).ToString(@"mm\:ss\:ff"),
                    new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleRight
                    },
                    GUILayout.Width(60));
            }
            EditorGUILayout.EndHorizontal();
        }

        private float DrawSlider(
            float startFrame,
            int maxFrame,
            Rect timeline,
            int imageWidth,
            float aspectRatio,
            ref float endFrame)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            {
                var prevStartFrame = startFrame;
                var prevEndFrame = endFrame;

                // Display the min and max frame sliders
                EditorGUILayout.MinMaxSlider(
                    ref startFrame,
                    ref endFrame,
                    0,
                    maxFrame,
                    GUILayout.ExpandWidth(true)
                );

                // Ensure startFrame and endFrame are within valid range
                startFrame = Mathf.RoundToInt(Mathf.Clamp(startFrame, 0, endFrame - 1));
                endFrame = Mathf.RoundToInt(Mathf.Clamp(endFrame, startFrame + 1, maxFrame));

                // Only draw the preview frames once they're loaded
                if (_allTexturesLoaded)
                {
                    IEnumerator DrawPreviewStartFrame()
                    {
                        _drawPreviewStartFrame = true;
                        yield return new EditorWaitForSeconds(1.0f);
                        _drawPreviewStartFrame = false;
                    }

                    IEnumerator DrawPreviewEndFrame()
                    {
                        _drawPreviewEndFrame = true;
                        yield return new EditorWaitForSeconds(1.0f);
                        _drawPreviewEndFrame = false;
                    }

                    if (!Mathf.Approximately(startFrame, prevStartFrame) && !_drawPreviewStartFrame)
                    {
                        EditorCoroutineUtility.StartCoroutine(DrawPreviewStartFrame(), this);
                    }

                    if (_drawPreviewStartFrame)
                    {
                        var imageX = timeline.width * startFrame / maxFrame;
                        var rect = new Rect(timeline.position + new Vector2(imageX, -150f), new Vector2(imageWidth * 2.5f, (imageWidth / aspectRatio) * 2.5f));

                        GUI.DrawTexture(rect, _frameTextures[GetTextureFrame(Mathf.RoundToInt(startFrame))], ScaleMode.ScaleToFit);
                    }

                    if(!Mathf.Approximately(endFrame, prevEndFrame) && !_drawPreviewEndFrame)
                    {
                        EditorCoroutineUtility.StartCoroutine(DrawPreviewEndFrame(), this);
                    }

                    if (_drawPreviewEndFrame)
                    {
                        var imageX = timeline.width * endFrame / maxFrame;
                        var rect = new Rect(timeline.position + new Vector2(imageX, -150f), new Vector2(imageWidth * 2.5f, (imageWidth / aspectRatio) * 2.5f));

                        GUI.DrawTexture(rect, _frameTextures[GetTextureFrame(Mathf.RoundToInt(endFrame))], ScaleMode.ScaleToFit);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            return startFrame;
        }

        private Rect DrawTimeline(
            Rect window,
            GUIStyle darkGrayBackground,
            int framesToSkip,
            int imageWidth,
            int imageHeight,
            float spacing,
            float aspectRatio)
        {
            var timeline = EditorGUILayout.BeginHorizontal(darkGrayBackground, GUILayout.Height(84), GUILayout.ExpandWidth(true));
            {
                EditorGUILayout.Space(0.01f, true);

                if( _frameTextures != null )
                {
                    for (int i = 0; i < _dataset.FrameCount; i += framesToSkip)
                    {
                        var frameTexture = _frameTextures[GetTextureFrame(i)];

                        if (frameTexture != null)
                        {
                            float xPos = i * (imageWidth + spacing);
                            GUI.DrawTexture(new Rect(window.x + 20 + xPos, window.y + 36, imageWidth, imageHeight), frameTexture, ScaleMode.ScaleToFit);
                        }
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            return timeline;
        }

        private void LoadFrameImage(int datasetIndex)
        {
            var frame = _dataset.Frames[datasetIndex];
            byte[] fileData = File.ReadAllBytes(
                Path.Combine(
                    PlaybackSettings.PlaybackDatasetPath,
                    frame.ImagePath));

            ProcessFrameTexture(fileData, frame, datasetIndex);
        }


        private void ProcessFrameTexture(byte[] fileData, PlaybackDataset.FrameMetadata frame, int datasetIndex)
        {
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(fileData);

            float rotationAngle = 0f;
            int width = texture.width;
            int height = texture.height;

            switch (frame.Orientation)
            {
                case ScreenOrientation.Portrait:
                    rotationAngle = Mathf.PI / 2; // 90 degrees
                    width = texture.height;
                    height = texture.width;
                    break;
                case ScreenOrientation.PortraitUpsideDown:
                    rotationAngle = 3 * Mathf.PI / 2; // 270 degrees
                    width = texture.height;
                    height = texture.width;
                    break;
                case ScreenOrientation.LandscapeLeft:
                    rotationAngle = 0f;
                    break;
                case ScreenOrientation.LandscapeRight:
                    rotationAngle = Mathf.PI; // 180 degrees
                    break;
                case ScreenOrientation.AutoRotation:
                case ScreenOrientation.Unknown:
                default:
                    break;
            }

            float aspectRatio = (float)width / height;
            height = 64;
            width = Mathf.RoundToInt(height * aspectRatio);

            RenderTexture rt = new RenderTexture(width, height, 0);
            Material mat = new Material(Shader.Find("Lightship/Editor/Custom/RotateTextureShader"));
            mat.SetFloat("_Rotation", rotationAngle);
            Graphics.Blit(texture, rt, mat);

            RenderTexture.active = rt;
            Texture2D rotatedTexture = new Texture2D(width, height);
            rotatedTexture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            rotatedTexture.Apply();
            RenderTexture.active = null;

            _frameTextures[GetTextureFrame(datasetIndex)] = rotatedTexture;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
            {
                pix[i] = col;
            }
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private IEnumerator LoadAllFrames()
        {
            // Load all textures
            for (int i = 0; i < _dataset.FrameCount; i++)
            {
                if (_frameTextures[i] != null)
                {
                    continue;
                }

                LoadFrameImage(i);

                _loadProgress = (float)i / _dataset.FrameCount * 100.0f;

                if(i % 5 == 0)
                {
                    yield return null;
                }
            }
        }

        private void LoadPreviewFrames()
        {
            var framesToSkip = _dataset.FrameCount <= PreviewFrameCount ?
                1 :
                _dataset.FrameCount / PreviewFrameCount;

            // Load preview textures
            for (int i = 0; i < _dataset.FrameCount; i += framesToSkip)
            {
                LoadFrameImage(i);
            }
        }

        private IEnumerator LoadFrameTextures()
        {
            if (_dataset == null)
            {
                yield break;
            }

            if (_frameTextures != null)
            {
                yield break;
            }

            // If the dataset is too long, only load the previews
            int frameCount = _dataset.FrameCount;
            if (_dataset.FrameCount > PreviewLengthThreshold)
            {
                _onlyLoadPreviews = true;
                frameCount = PreviewFrameCount;
            }

            _frameTextures = new Texture2D[frameCount];

            LoadPreviewFrames();

            _previewTexturesLoaded = true;

            if (!_onlyLoadPreviews)
                yield return LoadAllFrames();

            _allTexturesLoaded = true;
        }

        private int GetTextureFrame(int datasetFrame)
        {
            if (!_onlyLoadPreviews)
                return datasetFrame;

            var framesToSkip = _dataset.FrameCount <= PreviewFrameCount ?
                1 :
                _dataset.FrameCount / PreviewFrameCount;

            // If dataset.FrameCount doesn't evenly divide by PreviewFrameCount, ensure the remainder corresponds to the final preview frame
            return Math.Clamp((datasetFrame / framesToSkip), 0, PreviewFrameCount - 1);
        }
    }
}
