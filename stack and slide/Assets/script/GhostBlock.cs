using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GhostBlock : MonoBehaviour {
    public GameObject cBlock;
    Vector3 maxY;
    Block block;
    stage stage1;

    public float distanceY;
    RaycastHit hit;
    int layerMask;
    public bool OnInsideY;
    public Vector3 AdjustY;
    public Vector3 pre_AdjustY;
    bool BlockRotation;

    void Awake() {
        int layerMask = (-1) - (1 << LayerMask.NameToLayer("Block"));
        OnInsideY = false;
    }

    void Start() {
        block = GameObject.FindWithTag("Block").GetComponent<Block>();
        block.BlockRotation = GameObject.FindWithTag("Block").GetComponent<Block>().BlockRotation;
        stage1 = GameObject.Find("Stage").GetComponent<stage>();
    }




    //void OnCollisionEnter(Collision other) {
    //    if (other.gameObject.tag == "OnFixedBlock") {
    //        Destroy(this.gameObject);
    //    }



    //    if (other.gameObject.tag == "FixedBlock" || other.gameObject.tag == "Floor") {

    //        FixedY = true;
    //        transform.parent.position = cBlock.transform.parent.position - maxY + AdjustY;
    //        AdjustY += new Vector3(0, 0.1f, 0);
    //    } 

    //    if(other.gameObject == null) { FixedY = false; }
    //}

    void OnTriggerStay(Collider other) {
        if (other.gameObject.tag == "OnFixedBlock") {
            Destroy(this.gameObject);
        }


        if (other.gameObject.tag == "FixedBlock" || other.gameObject.tag == "Floor") {
            OnInsideY = true;
            AdjustY += new Vector3(0, 0.1f, 0);
            transform.parent.position = cBlock.transform.parent.position - maxY + AdjustY;
        }

    }

    void OnTriggerExit(Collider other) { 
        if (other.gameObject.tag == "FixedBlock" || other.gameObject.tag == "Floor") {
            OnInsideY = false;
            pre_AdjustY = AdjustY;
            

        }

}


    void Update() {
        cBlock = block.target;

        if (block.BlockRotation == true) {
            AdjustY = new Vector3(0,0,0);
            pre_AdjustY = new Vector3(0, 0, 0);
            Debug.Log("Reset");
            block.BlockRotation = false;
        }

        if (cBlock != null) {
            if (Physics.Raycast(cBlock.transform.position, new Vector3(0, 1, 0) * (-1), out hit, 20)) {
                if (hit.collider.gameObject.tag == "Floor" || hit.collider.gameObject.tag == "FixedBlock") {
                    distanceY = hit.distance;
                    maxY = new Vector3(0, distanceY, 0);
                }
            }

            transform.parent.rotation = cBlock.transform.parent.rotation;




            if (OnInsideY != true) {
                
                transform.parent.position = cBlock.transform.parent.position + pre_AdjustY - maxY;
                

            }
            if (OnInsideY == true) {
                //transform.parent.position = cBlock.transform.parent.position - maxY + pre_AdjustY;
                
                //AdjustY = new Vector3(0, 0, 0);
            }

            //FixedY = false;

        }
    }



}



