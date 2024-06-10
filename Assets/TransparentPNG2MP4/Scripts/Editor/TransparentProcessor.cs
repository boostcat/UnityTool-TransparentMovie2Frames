using PlasticPipe.PlasticProtocol.Messages;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;

public class TransparentProcessor : EditorWindow
{
    public enum OutMode
    {
        UpDown,
        LeftRight
    }

    private OutMode mode = OutMode.UpDown;
    private string ffmpegPath, movPath, movFramesPath, srcFolder, outFolder;
    private string[] files;
    private int currentIndex = 0;
    private int lineHeight = 40;
    private bool isExtracting, isProcessing;
    private GUIStyle yellowLabelStyle;

    [MenuItem("Tools/Transparent Processor")]
    public static void ShowWindow()
    {
        var window = GetWindow<TransparentProcessor>("Transparent Processor");
        window.minSize = new Vector2(500f, 650f);
        window.Show();
    }

    private void OnEnable()
    {
        movPath = "填入mov路径";
        movFramesPath = Path.GetDirectoryName(Application.dataPath) + "/Frames";
        ffmpegPath = Application.streamingAssetsPath + "/ffmpeg.exe";
        srcFolder = Path.GetDirectoryName(Application.dataPath) + "/Input";
        outFolder = Path.GetDirectoryName(Application.dataPath) + "/Output";
        yellowLabelStyle = new GUIStyle(EditorStyles.label);
        yellowLabelStyle.normal.textColor = Color.yellow;
    }

    private void OnDisable()
    {
        try
        {
            isProcessing = false;
            EditorApplication.update -= ProcessImages;
        }
        catch { }
    }

    void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("FFMpeg Path", EditorStyles.boldLabel);
        ffmpegPath = EditorGUILayout.TextArea(ffmpegPath, GUILayout.Height(lineHeight));

        GUILayout.Space(10);
        GUILayout.Label("Mov Setting", EditorStyles.boldLabel);

        GUILayout.Label("Mov Path", EditorStyles.label);
        movPath = EditorGUILayout.TextField(movPath, GUILayout.Height(lineHeight));
        if (GUILayout.Button("Select Video"))
        {
            string path = EditorUtility.OpenFilePanel("Select Video File", "", "mov,mp4");
            if (!string.IsNullOrEmpty(path))
            {
                movPath = path;
            }
        }

        GUILayout.Label("Frames Output Path", EditorStyles.label);
        movFramesPath = EditorGUILayout.TextField(movFramesPath, GUILayout.Height(lineHeight));
        if (GUILayout.Button("Select Frames Folder"))
        {
            string path = EditorUtility.OpenFolderPanel("Select Frames Folder", "", "");
            if (!string.IsNullOrEmpty(path))
            {
                movFramesPath = path;
            }
        }

        if (GUILayout.Button("Clear Frames Folder"))
        {
            ClearOutputFolder(movFramesPath);
        }

        if (GUILayout.Button("Extract Frames"))
        {
            if (string.IsNullOrEmpty(movPath) || string.IsNullOrEmpty(movFramesPath))
            {
                EditorUtility.DisplayDialog("Error", "Please select both video and output paths.", "OK");
            }
            else
            {
                isExtracting = true;
                ExtractFrames(movPath, movFramesPath);
            }
        }

        if (isExtracting)
        {
            GUILayout.Label($"正在解压...请不要操作.\n> {movFramesPath}", yellowLabelStyle);
        }

        GUILayout.Space(10);
        GUILayout.Label("Processing Setting", EditorStyles.boldLabel);

        mode = (OutMode)EditorGUILayout.EnumPopup("Out Mode", mode);
        GUILayout.Label("Source Folder:", EditorStyles.label);
        srcFolder = EditorGUILayout.TextArea(srcFolder, GUILayout.Height(lineHeight));
        if (GUILayout.Button("Select Source Folder"))
        {
            string path = EditorUtility.OpenFolderPanel("Select Source Folder", "", "");
            if (!string.IsNullOrEmpty(path))
            {
                srcFolder = path;
            }
        }
        GUILayout.Label("Output Folder:", EditorStyles.label);
        outFolder = EditorGUILayout.TextArea(outFolder, GUILayout.Height(lineHeight));
        if (GUILayout.Button("Select Output Folder"))
        {
            string path = EditorUtility.OpenFolderPanel("Select Output Folder", "", "");
            if (!string.IsNullOrEmpty(path))
            {
                outFolder = path;
            }
        }

        if (GUILayout.Button("1. Image Processing(图片通道分离)"))
        {
            var srcDirs = srcFolder.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
            var outDirs = outFolder.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

            if (srcDirs.Length > 0 && outDirs.Length > 0 && Directory.Exists(srcDirs[0]) && Directory.Exists(outDirs[0]))
            {
                files = Directory.GetFiles(srcDirs[0], "*.png");
                System.Array.Sort(files);
                currentIndex = 0;
                isProcessing = true;
                EditorApplication.update += ProcessImages;
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Source or Output folder does not exist or not specified!", "OK");
            }
        }

        if (GUILayout.Button("2. Video Processing(分离后合成mp4)"))
        {
            MergeImagesToVideo(outFolder.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries)[0], ffmpegPath);
        }

        if (GUILayout.Button("Image Resize(若图片通道分离后宽高不被2整除必选)"))
        {
            ResizeImagesInFolder(srcFolder.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries)[0]);
        }

        if (GUILayout.Button("Clear Output Folder"))
        {
            ClearOutputFolder(outFolder);
        }

        if (isProcessing)
        {
            GUILayout.Label($"Processing {currentIndex}/{files.Length}");
        }
    }

    private void ExtractFrames(string videoFilePath, string framesOutputPath)
    {
        if (!Directory.Exists(framesOutputPath))
        {
            Directory.CreateDirectory(framesOutputPath);
        }

        string ffmpegPath = Application.streamingAssetsPath + "/ffmpeg.exe";
        string arguments = $"-i \"{videoFilePath}\" \"{Path.Combine(framesOutputPath, "frame_%04d.png")}\"";

        Thread t = new Thread(() =>
        {
            ProcessStartInfo processInfo = new ProcessStartInfo(ffmpegPath, arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (Process process = new Process { StartInfo = processInfo })
            {
                process.OutputDataReceived += (sender, args) => UnityEngine.Debug.Log(args.Data);
                process.ErrorDataReceived += (sender, args) => UnityEngine.Debug.Log(args.Data);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                UnityEngine.Debug.Log("Frames extracted successfully.");
                isExtracting = false;
            }
        });
        t.Start();
    }

    void ClearOutputFolder(string outFolder)
    {
        var outDirs = outFolder.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (outDirs.Length > 0 && Directory.Exists(outDirs[0]))
        {
            string[] files = Directory.GetFiles(outDirs[0], "*.png");
            foreach (string file in files)
            {
                File.Delete(file);
            }
            EditorUtility.DisplayDialog("Success", "Output folder has been cleared.", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Error", "Output folder does not exist or not specified!", "OK");
        }
    }

    void ProcessImages()
    {
        if (currentIndex < files.Length)
        {
            string filePath = files[currentIndex];
            ProcessImage(filePath, outFolder.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries)[0]);
            UnityEngine.Debug.Log($"Processed {currentIndex}/{files.Length}: {Path.GetFileName(filePath)}");
            currentIndex++;

            Repaint();
        }
        else
        {
            isProcessing = false;
            EditorApplication.update -= ProcessImages;
            EditorUtility.DisplayDialog("Processing Complete", "All images have been processed.", "OK");
        }
    }

    void ProcessImage(string srcPath, string outDir)
    {
        byte[] srcBytes = File.ReadAllBytes(srcPath);
        Texture2D srcTexture = new Texture2D(2, 2);
        srcTexture.LoadImage(srcBytes);

        int width = srcTexture.width;
        int height = srcTexture.height;

        Texture2D outTexture;
        Color[] srcPixels = srcTexture.GetPixels();
        Color[] outPixels;

        if (mode == OutMode.UpDown)
        {
            outTexture = new Texture2D(width, height * 2);
            outPixels = new Color[width * height * 2];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color srcColor = srcPixels[x + y * width];
                    if (srcColor.r == 0 && srcColor.g == 0 && srcColor.b == 0 && srcColor.a == 0)
                    {
                        outPixels[x + y * width] = Color.black;
                        outPixels[x + (y + height) * width] = Color.black;
                    }
                    else
                    {
                        outPixels[x + y * width] = new Color(srcColor.a, srcColor.a, srcColor.a, 1);
                        outPixels[x + (y + height) * width] = new Color(srcColor.r, srcColor.g, srcColor.b, 1);
                    }
                }
            }
        }
        else
        {
            outTexture = new Texture2D(width * 2, height);
            outPixels = new Color[width * 2 * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color srcColor = srcPixels[x + y * width];
                    if (srcColor.r == 0 && srcColor.g == 0 && srcColor.b == 0 && srcColor.a == 0)
                    {
                        outPixels[x + y * width * 2] = Color.black;
                        outPixels[(x + width) + y * width * 2] = Color.black;
                    }
                    else
                    {
                        outPixels[x + y * width * 2] = new Color(srcColor.r, srcColor.g, srcColor.b, 1);
                        outPixels[(x + width) + y * width * 2] = new Color(srcColor.a, srcColor.a, srcColor.a, 1);
                    }
                }
            }
        }

        outTexture.SetPixels(outPixels);
        outTexture.Apply();

        byte[] outBytes = outTexture.EncodeToPNG();
        string outPath = Path.Combine(outDir, Path.GetFileName(srcPath));
        File.WriteAllBytes(outPath, outBytes);

        DestroyImmediate(srcTexture);
        DestroyImmediate(outTexture);
    }

    void MergeImagesToVideo(string outDir, string ffmpegPath)
    {
        string[] files = Directory.GetFiles(outDir, "*.png");
        if (files.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "No PNG files found in the output folder!", "OK");
            return;
        }

        // Ensure the files are sorted by name
        System.Array.Sort(files);

        string tempDir = Path.Combine(outDir, "tempOutImages");
        Directory.CreateDirectory(tempDir);

        for (int i = 0; i < files.Length; i++)
        {
            string srcFile = files[i];
            string destFile = Path.Combine(tempDir, $"{i:D4}.png");
            File.Copy(srcFile, destFile, true);
        }

        string arguments = $"-framerate 25 -i \"{tempDir}/%04d.png\" -c:v libx264 -pix_fmt yuv420p -hide_banner \"{outDir}/output_{mode}.mp4\"";
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process process = new Process { StartInfo = startInfo })
        {
            process.OutputDataReceived += (sender, args) => UnityEngine.Debug.Log(args.Data);
            process.ErrorDataReceived += (sender, args) => UnityEngine.Debug.Log(args.Data);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
        }

        Directory.Delete(tempDir, true);

        EditorUtility.DisplayDialog("FFmpeg Complete", "Video has been created.", "OK");
        UnityEngine.Debug.Log($"Video Location：{outDir}/output_{mode}.mp4");
    }

    private void ResizeImagesInFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            UnityEngine.Debug.Log("The specified folder does not exist.");
            return;
        }

        var pngFiles = Directory.GetFiles(folderPath, "*.png", SearchOption.AllDirectories);
        UnityEngine.Debug.Log($"Found {pngFiles.Length} PNG files.\n");

        foreach (var file in pngFiles)
        {
            ResizeImage(file);
        }

        AssetDatabase.Refresh();
    }

    private void ResizeImage(string filePath)
    {
        byte[] fileData = File.ReadAllBytes(filePath);
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(fileData);

        int newWidth = GetNextMultipleOf2(texture.width);
        int newHeight = GetNextMultipleOf2(texture.height);

        if (newWidth != texture.width || newHeight != texture.height)
        {
            Texture2D resizedTexture = ResizeTexture(texture, newWidth, newHeight);
            File.WriteAllBytes(filePath, resizedTexture.EncodeToPNG());
            UnityEngine.Debug.Log($"{Path.GetFileName(filePath)} resized to {newWidth}x{newHeight}\n");
            DestroyImmediate(resizedTexture);
        }
        else
        {
            UnityEngine.Debug.Log($"{Path.GetFileName(filePath)} is already of size {newWidth}x{newHeight}\n");
        }

        DestroyImmediate(texture);
    }

    private int GetNextMultipleOf2(int value)
    {
        return (value % 2 == 0) ? value : value + 1;
    }

    private Texture2D ResizeTexture(Texture2D source, int newWidth, int newHeight)
    {
        RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
        rt.filterMode = FilterMode.Bilinear;
        RenderTexture.active = rt;
        Graphics.Blit(source, rt);

        Texture2D newTexture = new Texture2D(newWidth, newHeight);
        newTexture.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        newTexture.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        return newTexture;
    }
}