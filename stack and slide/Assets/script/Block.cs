using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Block : MonoBehaviour {
    bool MoveOk;
    public GameObject target;
    public GameObject preTarget;
    bool left_Move2;
    public bool BlockRotation;
    string type;
    stage stage1;
    GhostBlock ghostBlock;
    float distanceY;
    bool UpOk;

    private void Awake() {
        ghostBlock = GameObject.FindGameObjectWithTag("GhostBlock").GetComponent<GhostBlock>();

    }
    void Update() {
        if (Input.GetMouseButtonDown(0)) {
            target = GameObject.FindGameObjectWithTag("Block");
            //if (target.Equals(gameObject) == true) {
            //} 
        } else if (target == null) {
            target = GameObject.FindGameObjectWithTag("Block");
        } else if (target.gameObject.tag == "OnFixedBlock") {
            target = GameObject.FindGameObjectWithTag("Block");
        }

        ControlBlock(type);
        
        if(target !=null && target.tag == "Block") {

            preTarget = GameObject.FindGameObjectWithTag("OnFixedBlock");
            if (preTarget != null) {
                preTarget.gameObject.tag = "FixedBlock";
                Destroy(this);
            }
        }
    }

    


    public void ControlBlock(string type) {
        if (target!=null && target.tag == "Block") {
            bool LType = (type == "L" ? true : false);
            if (Input.GetKeyDown(KeyCode.LeftArrow) == true || LType == true) {

                BlockRotation = true;
                target.transform.parent.Translate(Vector3.right*0.5f, Space.World); 
            }

            if (Input.GetKeyDown(KeyCode.RightArrow) == true) {
                BlockRotation = true;
                target.transform.parent.Translate(Vector3.left * 0.5f, Space.World);
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) == true) {
                BlockRotation = true;
                target.transform.parent.Translate(Vector3.back * 0.5f, Space.World);
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) == true) {
                BlockRotation = true;
                target.transform.parent.Translate(Vector3.forward * 0.5f, Space.World);
            }

            if (Input.GetKeyDown(KeyCode.Z) == true) {
                BlockRotation = true;
                target.transform.parent.Rotate(new Vector3(90, 0, 0), Space.World);

            }

            if (Input.GetKeyDown(KeyCode.X) == true) {
                BlockRotation = true;
                target.transform.parent.Rotate(new Vector3(0, 90, 0), Space.World);
            }

            if (Input.GetKeyDown(KeyCode.C) == true) {
                BlockRotation = true;
                target.transform.parent.Rotate(new Vector3(0, 0, 90), Space.World);
            }

            if (Input.GetKeyDown(KeyCode.A) == true) {
                BlockRotation = true;
                UpOk = true;
                target.transform.parent.Translate(Vector3.down * 0.5f, Space.World);
                CheckValid();
            }

            if (Input.GetKeyDown(KeyCode.S) == true) {
                BlockRotation = true;
                target.transform.parent.Translate(Vector3.up * 0.5f, Space.World);
            }
            
        }


    }



    //public GameObject GetClickedObject() {
    //    RaycastHit hit;

    //    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition); //마우스 포인트 근처 좌표를 만든다
    //    if (Physics.Raycast(ray.origin, ray.direction * 300, out hit) == true) {
    //        //있으면 오브젝트를 저장한다.
    //        if (hit.collider.gameObject.tag == "Block") {
    //            target = hit.collider.gameObject;
    //        }
    //    }
    //        return target;

    //}

    public void CheckValid() {
        ghostBlock = GameObject.FindGameObjectWithTag("GhostBlock").GetComponent<GhostBlock>();
        distanceY = GameObject.FindGameObjectWithTag("GhostBlock").GetComponent<GhostBlock>().distanceY-0.5f-ghostBlock.AdjustY.y;
        if (distanceY < 0.5f) { 
                    MoveOk = false;
                    target.gameObject.tag = "OnFixedBlock";
            target.transform.parent.Translate(new Vector3(0, -distanceY, 0), Space.World);
        }
    }


        //void OnCollisionStay(Collision other) {
        //    if (other.gameObject.tag == "Floor" || other.gameObject.tag == "FixedBlock") {
        //        MoveOk = false;
        //    if (target != null) {
        //        target.transform.localScale = new Vector3(1, 1, 1);
        //        target.gameObject.tag = "OnFixedBlock";
        //    }

                
            
        //    }
        //}

    void OnTriggerStay(Collider other) {
        if (other.gameObject.tag == "Floor" || other.gameObject.tag == "FixedBlock") {
            
            if (target != null) { 
                    target.gameObject.tag = "OnFixedBlock";
            }
            
            BlockUp();
        }
    }
    private void BlockUp() {
        if (UpOk == true) {
            transform.parent.Translate(Vector3.up * 0.5f, Space.World);
            UpOk = false;
        }
    }


}


