# Unity Barracuda Inference YOLOX
This Unity package extends the functionality of the [`barracuda-inference-base`](https://github.com/cj-mills/unity-barracuda-inference-base) package to perform object detection using YOLOX models. The package contains the `YOLOXObjectDetector` class, which handles model execution, processes the output, and generates bounding boxes with corresponding labels and colors. It supports various worker types and Asynchronous GPU Readback for model output.



## Demo Video

https://user-images.githubusercontent.com/9126128/230750644-6f234dfc-27dc-40e2-a354-286b1e38dcf6.mp4

## Demo Projects

* [barracuda-inference-yolox-demo](https://github.com/cj-mills/barracuda-inference-yolox-demo): A simple Unity project demonstrating how to perform object detection with the barracuda-inference-yolox package using a webcam.
* [barracuda-inference-yolox-demo-brp](https://github.com/cj-mills/barracuda-inference-yolox-demo-brp): A simple Unity BRP (Built-in Render Pipeline) project demonstrating how  to perform object detection with the barracuda-inference-yolox package using the in-game camera.
* [barracuda-inference-yolox-demo-urp](https://github.com/cj-mills/barracuda-inference-yolox-demo-urp): A simple Unity URP project demonstrating how to perform object detection with the barracuda-inference-yolox package using the in-game camera.
* [barracuda-inference-yolox-demo-hdrp](https://github.com/cj-mills/barracuda-inference-yolox-demo-hdrp): A simple Unity HDRP project demonstrating how to perform object  detection with the barracuda-inference-yolox package using the in-game  camera.

## Code Walkthrough
* [Code Walkthrough: Unity Barracuda Inference YOLOX Package](https://christianjmills.com/posts/unity-barracuda-inference-yolox-walkthrough/)


## Features

- Easy integration with YOLOX object detection models
- Utilizes Unity's Barracuda engine for efficient inference
- Supports various worker types and Asynchronous GPU Readback
- Processes output to generate bounding boxes with labels and colors


## Getting Started

### Prerequisites

- Unity game engine

### Installation

You can install the Barracuda Inference YOLOX package using the Unity Package Manager:

1. Open your Unity project.
2. Go to Window > Package Manager.
3. Click the "+" button in the top left corner, and choose "Add package from git URL..."
4. Enter the GitHub repository URL: `https://github.com/cj-mills/unity-barracuda-inference-yolox.git`
5. Click "Add". The package will be added to your project.

For Unity versions older than 2021.1, add the Git URL to the `manifest.json` file in your project's `Packages` folder as a dependency:

```json
{
  "dependencies": {
    "com.cj-mills.barracuda-inference-yolox": "https://github.com/cj-mills/unity-barracuda-inference-yolox.git",
    // other dependencies...
  }
}
```

## Train a Custom Model

If you're interested in training a YOLOX model on a custom dataset, check out my tutorial tutorial linked below:

- **[Training YOLOX Models for Real-Time Object Detection in Pytorch](https://christianjmills.com/series/tutorials/pytorch-train-object-detector-yolox-series.html)**

## License

This project is licensed under the MIT License. See the [LICENSE](Documentation~/LICENSE) file for details.
