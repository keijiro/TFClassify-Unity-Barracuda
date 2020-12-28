using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Barracuda = Unity.Barracuda;

sealed class CameraController : MonoBehaviour
{
    [SerializeField] Barracuda.NNModel _model = null;
    [SerializeField] UnityEngine.UI.RawImage _display = null;

    [SerializeField, HideInInspector] Mesh _mesh = null;
    [SerializeField, HideInInspector] Material _material = null;

    WebCamTexture _webcam;
    RenderTexture _cropped;
    Detector _detector;
    int _lastFrame = -1;

    void Start()
    {
        _webcam = new WebCamTexture();
        _webcam.Play();

        var size = Detector.IMAGE_SIZE;
        _cropped = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32);
        _display.texture = _cropped;

        _detector = new Detector(_model);
    }

    void OnDisable()
    {
        _detector?.Dispose();
        _detector = null;
    }

    void OnDestroy()
    {
        if (_webcam != null) Destroy(_webcam);
        if (_cropped != null) Destroy(_cropped);
    }

    void Update()
    {
        // Retrieve results from the detector if it's ready.
        var frame = Time.frameCount;
        if (_lastFrame >= 0 && Time.frameCount - _lastFrame > 1)
        {
            _detector.RetrieveResults(DrawBoundingBoxes);
            _lastFrame = -1;
        }

        // Input image cropping
        var aspect = (float)_webcam.height / _webcam.width;
        var scale = new Vector2(-aspect, -1);
        var offset = new Vector2(1 - aspect / 2, 1);
        Graphics.Blit(_webcam, _cropped, scale, offset);

        // Async readback request
        AsyncGPUReadback.Request
          (_cropped, 0, TextureFormat.RGBA32, OnCompleteReadback);
    }

    void OnCompleteReadback(AsyncGPUReadbackRequest request)
    {
         if (!request.hasError && _lastFrame < 0)
         {
             _detector?.StartDetection(request.GetData<Color32>());
             _lastFrame = Time.frameCount;
         }
    }

    void DrawBoundingBoxes(IList<BoundingBox> boxes)
    {
        foreach (var box in boxes)
        {
            var dim = box.Dimensions;
            var rcp = 1.0f / Detector.IMAGE_SIZE;
            var scale = new Vector3(dim.Width * rcp, dim.Height * rcp, 0);
            var pos = new Vector3(dim.X * rcp, dim.Y * rcp, 0) + scale / 2;
            var matrix = Matrix4x4.TRS(pos, Quaternion.identity, scale);
            Graphics.DrawMesh(_mesh, matrix, _material, 0);
        }
    }
}
