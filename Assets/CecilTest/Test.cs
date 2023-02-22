using UnityEngine;
using System.Collections;

public class Test : MonoBehaviour 
{
    private void Start()
    {
		Debug.LogError("Test Start");
        
        this.LogTest();
    }

    [Log]
    private void LogTest()
    {
        Debug.Log( "Here's some logic" );
    }
}
