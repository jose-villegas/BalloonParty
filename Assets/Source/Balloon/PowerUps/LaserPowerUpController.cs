using UnityEngine;

public class LaserPowerUpController : BalloonPowerUpController
{
    [SerializeField] private GameObject _rotatingBody;
    [SerializeField] private float _rotationSpeed;
    
    private float _angle;

    public override void Activate()
    {
        // stop rotation 
        _rotationSpeed = 0f;
        
        // create laser range
        var e = _contexts.game.CreateEntity();
        e.AddAsset("LaserRange");
        e.AddPosition(_rotatingBody.transform.position);      
        e.AddRotation(_rotatingBody.transform.rotation);
        e.isBalloonCollider = transform;
    }

    private void Update()
    {
        _angle += _rotationSpeed * Time.deltaTime;
        _rotatingBody.transform.rotation = Quaternion.AngleAxis(_angle, Vector3.forward);
    }
}