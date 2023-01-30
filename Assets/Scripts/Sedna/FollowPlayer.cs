using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class FollowPlayer : MonoBehaviour
{
    [SerializeField] Transform _positionSednaTarget;
    [SerializeField] Transform _kayakTransform;

    private Rigidbody _rigidbody;
    private Vector3 _velocity;

    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }
    void LateUpdate()
    {
        Position();
        Rotation();
    }
    private void Position()
    {
        Vector3 current = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 target = new Vector3(_positionSednaTarget.transform.position.x, 0, _positionSednaTarget.transform.position.z);
        Vector3 pos = Vector3.SmoothDamp(current, target, ref _velocity, 0.5f);

        Vector3 velocity = new Vector3(_velocity.x, _rigidbody.velocity.y, _velocity.z);

        _rigidbody.velocity = velocity;
    }
    private void Rotation()
    {
        Quaternion targetQuaternion = Quaternion.Euler(new Vector3(0, _kayakTransform.rotation.eulerAngles.y, 0));

        Quaternion rota = Quaternion.Slerp(_kayakTransform.rotation, targetQuaternion, 0.5f);

        _rigidbody.MoveRotation(rota);
    }
}
