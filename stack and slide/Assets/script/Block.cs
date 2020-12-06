using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Block : MonoBehaviour {
    bool MoveOk;
    public GameObject target;
    public GameObject preTarget;
    bool left_Move2;
    string type;
    stage stage1;


    private void Awake() {


    }
    void Update() {
        if (Input.GetMouseButtonDown(0)) {
            target = GetClickedObject();
            if (target.Equals(gameObject) == true) {
            } 
        } else if (target == null) {
            target = GameObject.FindGameObjectWithTag("Block");
        } else if (target.gameObject.tag == "OnFixedBlock") {
            target = GameObject.FindGameObjectWithTag("Block");
        }

        ControlBlock(type);
        if(target != this.gameObject) {
            preTarget = GameObject.FindGameObjectWithTag("OnFixedBlock");
            preTarget.gameObject.tag = "FixedBlock";
            Destroy(this);
        }
    }




    public void ControlBlock(string type) {
        if (target.tag == "Block") {

            bool LType = (type == "L" ? true : false);
            if (Input.GetKeyDown(KeyCode.LeftArrow) == true || LType == true) {
                target.transform.parent.Translate(Vector3.right, Space.World);
            }

            if (Input.GetKeyDown(KeyCode.RightArrow) == true) {
                target.transform.parent.Translate(Vector3.left, Space.World);
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) == true) {
                target.transform.parent.Translate(Vector3.back, Space.World);
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) == true) {
                target.transform.parent.Translate(Vector3.forward, Space.World);
            }

            if (Input.GetKeyDown(KeyCode.Z) == true) {
                target.transform.parent.Rotate(new Vector3(90, 0, 0), Space.World);
            }

            if (Input.GetKeyDown(KeyCode.X) == true) {
                target.transform.parent.Rotate(new Vector3(0, 90, 0), Space.World);
            }

            if (Input.GetKeyDown(KeyCode.C) == true) {
                target.transform.parent.Rotate(new Vector3(0, 0, 90), Space.World);
            }

            if (Input.GetKeyDown(KeyCode.A) == true) {
                target.transform.parent.Translate(Vector3.down, Space.World);
            }

            if (Input.GetKeyDown(KeyCode.S) == true) {
                target.transform.parent.Translate(Vector3.up, Space.World);
            }
        }
    }



    public GameObject GetClickedObject() {
        RaycastHit hit;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition); //마우스 포인트 근처 좌표를 만든다
        if (Physics.Raycast(ray.origin, ray.direction * 300, out hit) == true) {
            //있으면 오브젝트를 저장한다.
            if (hit.collider.gameObject.tag == "Block") {
                target = hit.collider.gameObject;
            }
        }
            return target;
        
    }





        void OnCollisionEnter(Collision other) {
            if (other.gameObject.tag == "Floor" || other.gameObject.tag == "FixedBlock") {
                MoveOk = false;
                target.gameObject.tag = "OnFixedBlock";
        }
        }

        void OnTriggerStay(Collider other) {
            if (other.gameObject.tag == "Floor" || other.gameObject.tag == "FixedBlock") {
                MoveOk = false;
                target.gameObject.tag = "OnFixedBlock";
        }
        }




    }


