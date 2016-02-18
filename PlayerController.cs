using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour {

    public float rotationSpeed;
    public float forwardSpeed;
    private CharacterController playerController;

	// Use this for initialization
	void Start () {
        playerController = GetComponent<CharacterController>();
	}
	
	// Update is called once per frame
	void Update () {
        if (Input.GetKeyUp("space") && playerController.isGrounded)
        {
            playerController.Move(Vector3.up);
        }
        transform.Rotate(0, Input.GetAxis("Horizontal") * rotationSpeed, 0);
        //transform.Rotate(0, 1 * rotationSpeed / 10, 0);
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        float speed = forwardSpeed * Input.GetAxis("Vertical");
        playerController.SimpleMove(forward * speed);
        //playerController.SimpleMove(forward*forwardSpeed);
	}
}
