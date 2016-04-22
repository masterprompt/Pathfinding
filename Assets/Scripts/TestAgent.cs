using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using Endboss.Navigation;

[AddComponentMenu("Scripts/TestAgent")]
public class TestAgent : MonoBehaviour
{

    #region Properties
    public Transform target = null;
    public Agent agent = new Agent();
    public float speed = 4;
    #endregion

    #region MonoBehaviour
    void Awake()
    {

    }
    void Start()
    {

    }
    void Update()
    {
        transform.position += agent.Vector3ToPoint(this.transform, target).normalized * (speed * Time.deltaTime);
    }
    #endregion
}
