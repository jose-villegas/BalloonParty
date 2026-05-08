using UnityEngine;

[RequireComponent(typeof(Camera))]
public class OrthogonalSizeCameraController : MonoBehaviour
{
    private Camera _camera;
    private IGameConfiguration _configuration;

    private void Start()
    {
        _camera = GetComponent<Camera>();
        _configuration = Contexts.sharedInstance.configuration.gameConfiguration.value;

        var size = _configuration.DisplayConfiguration.GetOrthogonalSize();
        _camera.orthographicSize = size < 0 ? _camera.orthographicSize : size;
    }
}