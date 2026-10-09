# Code Walkthrough: Unity Barracuda Inference YOLOX Package

> [!NOTE]
> **Retired from [christianjmills.com](https://christianjmills.com) on 2026-10-09.** This post was first published on 2023-05-06 at <https://christianjmills.com/posts/unity-barracuda-inference-yolox-walkthrough/>, which now redirects here. It is kept as it was written; its tools and steps may be out of date.
>
> Source: [`posts/unity-barracuda-inference-yolox-walkthrough/index.md@8229e99`](https://github.com/cj-mills/christianjmills/blob/8229e991c0be7f8c78a909f0d8a2ca74390dc1e6/posts/unity-barracuda-inference-yolox-walkthrough/index.md) · Comments: <https://github.com/cj-mills/christianjmills/issues/42>

*Walk through the code for the Unity Barracuda Inference YOLOX package, which extends the functionality of `unity-barracuda-inference-base` to perform object detection using YOLOX models.*

- [Introduction](#introduction)
- [Package Overview](#package-overview)
- [Code Explanation](#code-explanation)
- [Conclusion](#conclusion)

## Introduction

The [Barracuda Inference YOLOX](https://github.com/cj-mills/unity-barracuda-inference-yolox) package extends the functionality of [`unity-barracuda-inference-base`](https://github.com/cj-mills/unity-barracuda-inference-base) to perform object detection using [YOLOX](https://github.com/Megvii-BaseDetection/YOLOX) models.

Object detection has numerous potential uses in Unity applications, including giving NPCs a more realistic perception of their environment, gesture-based controls, and augmented reality, to name a few. Here is a demo video from a project that uses this package to detect hand gestures.

[▶ Video: barracuda-inference-yolox-demo](./videos/barracuda-inference-yolox-demo.mp4)

In this post, I’ll walk through the package code, providing a solid understanding of its components and their roles.

## Package Overview

The package contains two C# scripts.

1.  `YOLOXObjectDetector.cs`: This script provides functionality to perform object detection with YOLOX models using the Barracuda inference engine.
2.  `PackageInstaller.cs`: An Editor utility script for automatically installing a list of dependency packages defined in a JSON file.

## Code Explanation

In this section, we will delve deeper into the Barracuda Inference YOLOX package by examining the purpose and functionality of each C# script.

### `YOLOXObjectDetector.cs`

This script defines the `YOLOXObjectDetector` class, which extends the `BarracudaModelRunner` class from the Barracuda Inference Base package to perform object detection using YOLOX models. This class also depends on the [`bounding-box-2d-toolkit`](https://github.com/cj-mills/unity-bounding-box-2d-toolkit) and [`unity-yolox-utils`](https://github.com/cj-mills/unity-yolox-utils) packages. The complete code is available on GitHub at the link below.

- [YOLOXObjectDetector.cs](https://github.com/cj-mills/unity-barracuda-inference-yolox/blob/main/Runtime/Scripts/YOLOXObjectDetector.cs)

#### Serialized Fields

The `YOLOXObjectDetector` class includes a field to add a color map from a JSON file.

``` c#
// Output Processing configuration and variables
[Header("Output Processing")]
// JSON file containing the color map for bounding boxes
[SerializeField, Tooltip("JSON file with bounding box colormaps")]
private TextAsset colormapFile;
```

It also includes a field to control how often to unload memory assets when using Barracuda’s Pixel Shader backend. The Pixel Shader backend enables GPU inference on platforms that don’t support Compute Shaders. However, there seems to be a bug in the current version of Barracuda, which does not release unused assets when using this backend. Left unchecked, this can fill up both system and GPU memory. We can address this by manually freeing memory. Doing that every frame can hurt performance, so we’ll only do it at set intervals.

``` c#
[Header("Settings")]
[Tooltip("Interval (in frames) for unloading unused assets with Pixel Shader backend")]
[SerializeField] private int pixelShaderUnloadInterval = 100;
```

#### Serializable Classes

The `Colormap` and `ColormapList` classes help store color map information from a JSON file, which we then use for generating bounding boxes.

``` c#
// Serializable classes to store color map information from JSON
[System.Serializable]
class Colormap
{
    public string label;
    public List<float> color;
}

[System.Serializable]
class ColormapList
{
    public List<Colormap> items;
}
```

#### Private Variables

``` c#
// A counter for the number of frames processed.
private int frameCounter = 0;

// Indicates if the system supports asynchronous GPU readback
private bool supportsAsyncGPUReadback = false;

// Stride values used by the YOLOX model
private static readonly int[] Strides = { 8, 16, 32 };

// Number of fields in each bounding box
private const int NumBBoxFields = 5;

// Layer names for the Transpose, Flatten, and TransposeOutput operations
private const string TransposeLayer = "transpose";
private const string FlattenLayer = "flatten";
private const string TransposeOutputLayer = "transposeOutput";
private string defaultOutputLayer;

// Texture formats for output processing
private TextureFormat textureFormat = TextureFormat.RHalf;
private RenderTextureFormat renderTextureFormat = RenderTextureFormat.RHalf;

// List to store label and color pairs for each class
private List<(string, Color)> colormapList = new List<(string, Color)>();

// Output textures for processing on CPU and GPU
private Texture2D outputTextureCPU;
private RenderTexture outputTextureGPU;

// List to store grid and stride information for the YOLOX model
private List<GridCoordinateAndStride> gridCoordsAndStrides = new List<GridCoordinateAndStride>();

// Length of the proposal array for YOLOX output
private int proposalLength;
```

#### `Start`

This method runs at the start of the script. It performs several initializations, including checking for async GPU readback support, loading the color map list, and initializing the output texture.

``` c#
// Called at the start of the script
protected override void Start()
{
    base.Start();
    CheckAsyncGPUReadbackSupport(); // Check if async GPU readback is supported
    LoadColorMapList(); // Load colormap information from JSON file
    CreateOutputTexture(1, 1); // Initialize output texture

    proposalLength = colormapList.Count + NumBBoxFields; // Calculate proposal length
}
```

#### `CheckAsyncGPUReadbackSupport`

This method checks if the system supports asynchronous GPU readback

``` c#
// Check if the system supports async GPU readback
public bool CheckAsyncGPUReadbackSupport()
{
    supportsAsyncGPUReadback = SystemInfo.supportsAsyncGPUReadback && supportsAsyncGPUReadback;
    return supportsAsyncGPUReadback;
}
```

#### `LoadAndPrepareModel`

This method loads and prepares the YOLOX model by setting worker types and applying transpose and flatten operations.

``` c#
// Load and prepare the YOLOX model
protected override void LoadAndPrepareModel()
{
    base.LoadAndPrepareModel();

    defaultOutputLayer = modelBuilder.model.outputs[0];
    WorkerFactory.Type bestType = WorkerFactory.ValidateType(WorkerFactory.Type.Auto);
    bool supportsComputeBackend = bestType == WorkerFactory.Type.ComputePrecompiled;

    // Set worker type for WebGL
    if (Application.platform == RuntimePlatform.WebGLPlayer)
    {
        workerType = WorkerFactory.Type.PixelShader;
    }

    // Apply transpose operation on the output layer
    modelBuilder.Transpose(TransposeLayer, defaultOutputLayer, new[] { 0, 3, 2, 1, });
    defaultOutputLayer = TransposeLayer;

    // Apply Flatten and TransposeOutput operations if supported
    if (supportsComputeBackend && (workerType != WorkerFactory.Type.PixelShader))
    {
        modelBuilder.Flatten(FlattenLayer, TransposeLayer);
        modelBuilder.Transpose(TransposeOutputLayer, FlattenLayer, new[] { 0, 1, 3, 2 });
        modelBuilder.Output(TransposeLayer);
        defaultOutputLayer = TransposeOutputLayer;
    }
}
```

#### `InitializeEngine`

This method initializes the Barracuda engine and checks if asynchronous GPU readback is supported.

``` c#
/// <summary>
/// Initialize the Barracuda engine
/// <summary>
protected override void InitializeEngine()
{
    base.InitializeEngine();

    // Check if async GPU readback is supported by the engine
    supportsAsyncGPUReadback = engine.Summary().Contains("Unity.Barracuda.ComputeVarsWithSharedModel");
}
```

#### `LoadColorMapList`

This method loads the color map list from a JSON file.

``` c#
/// <summary>
/// Load the color map list from the JSON file
/// <summary>
private void LoadColorMapList()
{
    if (IsColorMapListJsonNullOrEmpty())
    {
        Debug.LogError("Class labels JSON is null or empty.");
        return;
    }

    ColormapList colormapObj = DeserializeColorMapList(colormapFile.text);
    UpdateColorMap(colormapObj);
}
```

#### `IsColorMapListJsonNullOrEmpty`

This method checks if the provided color map JSON file is null or empty.

``` c#
/// <summary>
/// Check if the color map JSON file is null or empty
/// <summary>
private bool IsColorMapListJsonNullOrEmpty()
{
    return colormapFile == null || string.IsNullOrWhiteSpace(colormapFile.text);
}
```

#### `DeserializeColorMapList`

This method deserializes the provided color map JSON string to a `ColormapList` object.

``` c#
/// <summary>
/// Deserialize the color map list from the JSON string
/// <summary>
private ColormapList DeserializeColorMapList(string json)
{
    try
    {
        return JsonUtility.FromJson<ColormapList>(json);
    }
    catch (Exception ex)
    {
        Debug.LogError($"Failed to deserialize class labels JSON: {ex.Message}");
        return null;
    }
}
```

#### `UpdateColorMap`

This method updates the `colormapList` array with the provided `ColormapList` object.

``` c#
/// <summary>
/// Update the color map list with deserialized data
/// <summary>
private void UpdateColorMap(ColormapList colormapObj)
{
    if (colormapObj == null)
    {
        return;
    }

    // Add label and color pairs to the colormap list
    foreach (Colormap colormap in colormapObj.items)
    {
        Color color = new Color(colormap.color[0], colormap.color[1], colormap.color[2]);
        colormapList.Add((colormap.label, color));
    }
}
```

#### `CreateOutputTexture`

This method creates an output texture with the specified width and height.

``` c#
/// <summary>
/// Create an output texture with the specified width and height.
/// </summary>
private void CreateOutputTexture(int width, int height)
{
    outputTextureCPU = new Texture2D(width, height, textureFormat, false);
}
```

#### `ExecuteModel`

This method executes the YOLOX model with a given input texture.

``` c#
/// <summary>
/// Execute the YOLOX model with the given input texture.
/// </summary>
public void ExecuteModel(RenderTexture inputTexture)
{
    using (Tensor input = new Tensor(inputTexture, channels: 3))
    {
        base.ExecuteModel(input);
    }

    // Update grid_strides if necessary
    if (engine.PeekOutput(defaultOutputLayer).length / proposalLength != gridCoordsAndStrides.Count)
    {
        gridCoordsAndStrides = YOLOXUtility.GenerateGridCoordinatesWithStrides(Strides, inputTexture.height, inputTexture.width);
    }
}
```

#### `ProcessOutput`

This method processes the output array from the YOLOX model, applying Non-Maximum Suppression (NMS), and returns an array of BBox2DInfo objects with class labels and colors.

``` c#
/// <summary>
/// Process the output array from the YOLOX model, applying Non-Maximum Suppression (NMS) and
/// returning an array of BBox2DInfo objects with class labels and colors.
/// </summary>
/// <param name="outputArray">The output array from the YOLOX model</param>
/// <param name="confidenceThreshold">The minimum confidence score for a bounding box to be considered</param>
/// <param name="nms_threshold">The threshold for Non-Maximum Suppression (NMS)</param>
/// <returns>An array of BBox2DInfo objects containing the filtered bounding boxes, class labels, and colors</returns>
public BBox2DInfo[] ProcessOutput(float[] outputArray, float confidenceThreshold = 0.5f, float nms_threshold = 0.45f)
{
    // Generate bounding box proposals from the output array
    List<BBox2D> proposals = YOLOXUtility.GenerateBoundingBoxProposals(outputArray, gridCoordsAndStrides, colormapList.Count, NumBBoxFields, confidenceThreshold);

    // Apply Non-Maximum Suppression (NMS) to the proposals
    List<int> proposal_indices = BBox2DUtility.NMSSortedBoxes(proposals, nms_threshold);

    // Create an array of BBox2DInfo objects containing the filtered bounding boxes, class labels, and colors
    return proposal_indices
        .Select(index => proposals[index])
        .Select(bbox => new BBox2DInfo(bbox, colormapList[bbox.index].Item1, colormapList[bbox.index].Item2))
        .ToArray();
}
```

#### `CopyOutputToArray`

This method copies the model output to a float array.

``` c#
/// <summary>
/// Copy the model output to a float array.
/// </summary>
public float[] CopyOutputToArray()
{
    using (Tensor output = engine.PeekOutput(defaultOutputLayer))
    {
        if (workerType == WorkerFactory.Type.PixelShader)
        {
            frameCounter++;
            if (frameCounter % pixelShaderUnloadInterval == 0)
            {
                Resources.UnloadUnusedAssets();
                frameCounter = 0;
            }
        }
        return output.data.Download(output.shape);
    }
}
```

#### `CopyOutputToTexture`

This method copies the model output to a texture.

``` c#
/// <summary>
/// Copy the model output to a texture.
/// </summary>
public void CopyOutputToTexture()
{
    using (Tensor output = engine.PeekOutput(TransposeLayer))
    {
        if (output.width != outputTextureCPU.width || output.height != outputTextureCPU.height)
        {
            CreateOutputTexture(output.width, output.height);
            outputTextureGPU = RenderTexture.GetTemporary(output.width, output.height, 0, renderTextureFormat);
        }
        output.ToRenderTexture(outputTextureGPU);
    }
}
```

#### `CopyOutputWithAsyncReadback`

This method copies the model output using asynchronous GPU readback if the platform supports it.

``` c#
/// <summary>
/// Copy the model output using async GPU readback. If not supported, defaults to synchronous readback.
/// </summary>
public float[] CopyOutputWithAsyncReadback()
{
    if (!supportsAsyncGPUReadback)
    {
        Debug.Log("Async GPU Readback not supported. Defaulting to synchronous readback");
        return CopyOutputToArray();
    }

    CopyOutputToTexture();

    AsyncGPUReadback.Request(outputTextureGPU, 0, textureFormat, OnCompleteReadback);

    Color[] outputColors = outputTextureCPU.GetPixels();
    float[] outputArray = outputColors.Select(color => color.r).Reverse().ToArray();

    // Reverse the order of each proposal in the output array
    for (int i = 0; i < outputArray.Length; i += proposalLength)
    {
        Array.Reverse(outputArray, i, proposalLength);
    }

    return outputArray;
}
```

#### `CropInputDims`

This method crops input dimensions to be divisible by the maximum stride.

``` c#
/// <summary>
/// Crop input dimensions to be divisible by the maximum stride.
/// </summary>
public Vector2Int CropInputDims(Vector2Int inputDims)
{
    inputDims[0] -= inputDims[0] % Strides.Max();
    inputDims[1] -= inputDims[1] % Strides.Max();

    return inputDims;
}
```

#### `OnCompleteReadback`

This method handles the completion of an async GPU readback request.

``` c#
/// <summary>
/// Handle the completion of an async GPU readback request.
/// </summary>
private void OnCompleteReadback(AsyncGPUReadbackRequest request)
{
    if (request.hasError)
    {
        Debug.Log("GPU readback error detected.");
        return;
    }

    if (outputTextureCPU != null)
    {
        try
        {
            // Load readback data into the output texture and apply changes
            outputTextureCPU.LoadRawTextureData(request.GetData<uint>());
            outputTextureCPU.Apply();
        }
        catch (UnityException ex)
        {
            if (ex.Message.Contains("LoadRawTextureData: not enough data provided (will result in overread)."))
            {
                Debug.Log("Updating input data size to match the texture size.");
            }
            else
            {
                Debug.LogError($"Unexpected UnityException: {ex.Message}");
            }
        }
    }
}
```

#### `OnDisable`

This method cleans up resources when the script is disabled.

``` c#
/// <summary>
/// Clean up resources when the script is disabled.
/// </summary>
protected override void OnDisable()
{
    base.OnDisable();
    // Release the temporary render texture
    RenderTexture.ReleaseTemporary(outputTextureGPU);
}
```

------------------------------------------------------------------------

### `PackageInstaller.cs`

In this section, we will go through the `PackageInstaller.cs` script and explain how each part of the code works to install the required packages. The complete code is available on GitHub at the link below.

- [PackageInstaller.cs](https://github.com/cj-mills/unity-barracuda-inference-yolox/blob/main/Editor/PackageInstaller.cs)

#### Serializable Classes

The script defines two serializable classes to hold package data.

``` c#
// Serializable class to hold package data
[System.Serializable]
public class PackageData
{
    public string packageName;
    public string packageUrl;
}

// Serializable class to hold a list of PackageData objects
[System.Serializable]
public class PackageList
{
    public List<PackageData> packages;
}
```

These classes are for deserializing the JSON file containing the list of packages to install.

#### `PackageInstaller` Class Variables

The `PackageInstaller` class contains several private static fields.

``` c#
// Stores the AddRequest object for the current package to install.
private static AddRequest addRequest;
// A list of PackageData objects to install.
private static List<PackageData> packagesToInstall;
// The index of the current package to install.
private static int currentPackageIndex;

// GUID of the JSON file containing the list of packages to install
private const string PackagesJSONGUID = "02aec9cd479b4b758a7afde0032230ec";
```

#### `InstallDependencies`

The `InstallDependencies()` method executes when Unity loads without action from the user. It reads the package JSON file and calls the `InstallNextPackage()` method to install the packages.

``` c#
// Method called on load to install packages from the JSON file
[InitializeOnLoadMethod]
public static void InstallDependencies()
{
    // Read the package JSON file
    packagesToInstall = ReadPackageJson().packages;
    // Initialize the current package index
    currentPackageIndex = 0;
    // Start installing the packages
    InstallNextPackage();
}
```

#### `InstallNextPackage`

This method installs the next package in the list.

``` c#
// Method to install the next package in the list
private static void InstallNextPackage()
{
    // Iterate through package list
    if (currentPackageIndex < packagesToInstall.Count)
    {
        PackageData packageData = packagesToInstall[currentPackageIndex];

        // Check if the package is already installed
        if (!IsPackageInstalled(packageData.packageName))
        {
            // Attempt to install package
            addRequest = Client.Add(packageData.packageUrl);
            EditorApplication.update += PackageInstallationProgress;
        }
        else
        {
            // Increment the current package index
            currentPackageIndex++;
            // Recursively call InstallNextPackage
            InstallNextPackage();
        }
    }
}
```

#### `PackageInstallationProgress`

This method monitors the progress of the package installation and logs whether it was successful. It then triggers the installation process for the next package in the list.

``` c#
// Method to monitor the progress of package installation
private static void PackageInstallationProgress()
{
    if (addRequest.IsCompleted)
    {
        // Log whether the package installation was successful
        if (addRequest.Status == StatusCode.Success)
        {
            UnityEngine.Debug.Log($"Successfully installed: {addRequest.Result.packageId}");
        }
        else if (addRequest.Status >= StatusCode.Failure)
        {
            UnityEngine.Debug.LogError($"Failed to install package: {addRequest.Error.message}");
        }

        // Unregister the method from the EditorApplication.update 
        EditorApplication.update -= PackageInstallationProgress;
        // Increment the current package index
        currentPackageIndex++;
        // Install the next package in the list
        InstallNextPackage();
    }
}
```

#### `IsPackageInstalled`

This method verifies whether a package has already been installed or not.

``` c#
// Method to check if a package is already installed
private static bool IsPackageInstalled(string packageName)
{
    // List the installed packages
    var listRequest = Client.List(true, false);
    while (!listRequest.IsCompleted) { }

    if (listRequest.Status == StatusCode.Success)
    {
        // Check if the package is already installed
        return listRequest.Result.Any(package => package.name == packageName);
    }
    else
    {
        UnityEngine.Debug.LogError($"Failed to list packages: {listRequest.Error.message}");
    }

    return false;
}
```

#### `ReadPackageJson`

This method reads the JSON file containing the list of packages to install and returns a `PackageList` object.

``` c#
// Method to read the JSON file and return a PackageList object
private static PackageList ReadPackageJson()
{
    // Convert the PackagesJSONGUID to an asset path
    string assetPath = AssetDatabase.GUIDToAssetPath(PackagesJSONGUID);
    // Read the JSON file content as a string
    string jsonString = File.ReadAllText(assetPath);
    // Deserialize the JSON string into a PackageList object
    return JsonUtility.FromJson<PackageList>(jsonString);
}
```

## Conclusion

This post provided an in-depth walkthrough of the code for the Barracuda Inference YOLOX package. The package extends the functionality of [`unity-barracuda-inference-base`](https://github.com/cj-mills/unity-barracuda-inference-base) to perform object detection using YOLOX models.

You can continue to explore the package by going to its GitHub repository linked below, where you will also find instructions for installing it using the Unity Package Manager.

- GitHub Repository: [unity-barracuda-inference-yolox](https://github.com/cj-mills/unity-barracuda-inference-yolox)

You can find the code for the demo project shown in the video at the beginning of this post linked below, along with other demos.

- [barracuda-inference-yolox-demo](https://github.com/cj-mills/barracuda-inference-yolox-demo): A simple Unity project demonstrating how to perform object detection with the barracuda-inference-yolox package using a webcam.
- [barracuda-inference-yolox-demo-brp](https://github.com/cj-mills/barracuda-inference-yolox-demo-brp): A simple Unity BRP (Built-in Render Pipeline) project demonstrating how to perform object detection with the barracuda-inference-yolox package using the in-game camera.
- [barracuda-inference-yolox-demo-urp](https://github.com/cj-mills/barracuda-inference-yolox-demo-urp): A simple Unity URP project demonstrating how to perform object detection with the barracuda-inference-yolox package using the in-game camera.
- [barracuda-inference-yolox-demo-hdrp](https://github.com/cj-mills/barracuda-inference-yolox-demo-hdrp): A simple Unity HDRP project demonstrating how to perform object detection with the barracuda-inference-yolox package using the in-game camera.
