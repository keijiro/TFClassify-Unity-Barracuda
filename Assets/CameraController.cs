using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

sealed class CameraController : MonoBehaviour
{
    [SerializeField] UnityEngine.UI.RawImage _display = null;
    [SerializeField] Detector _detector = null;

    [SerializeField] Mesh _mesh = null;
    [SerializeField] Material _material = null;

    WebCamTexture _webcam;
    RenderTexture _cropped;

    Queue<Matrix4x4> _boxes = new Queue<Matrix4x4>();

    void Start()
    {
        _webcam = new WebCamTexture();
        _webcam.Play();

        var width = Detector.IMAGE_SIZE;
        _cropped = new RenderTexture
          (width, width, 0, RenderTextureFormat.ARGB32);

        _display.texture = _cropped;
    }

    void Update()
    {
        var aspect = (float)_webcam.height / _webcam.width;
        var scale = new Vector2(-aspect, -1);
        var offset = new Vector2(1 - aspect / 2, 1);
        Graphics.Blit(_webcam, _cropped, scale, offset);

        AsyncGPUReadback.Request
          (_cropped, 0, TextureFormat.RGBA32, OnCompleteReadback);

         _detector.RetrieveResults( boxes => {
             foreach (var box in boxes)
             {
                 var dim = box.Dimensions;
                 var rcp = 1.0f / Detector.IMAGE_SIZE;
                 var scale = new Vector3(dim.Width * rcp, dim.Height * rcp, 0);
                 var pos = new Vector3(dim.X * rcp, dim.Y * rcp, 0) + scale / 2;
                 var matrix = Matrix4x4.TRS(pos, Quaternion.identity, scale);
                 _boxes.Enqueue(matrix);
             }
         });


        while (_boxes.Count > 0)
            Graphics.DrawMesh(_mesh, _boxes.Dequeue(), _material, 0);
    }

    void OnCompleteReadback(AsyncGPUReadbackRequest request)
    {
         if (request.hasError) return;
         _detector.IssueDetection(request.GetData<Color32>());
    }
}
