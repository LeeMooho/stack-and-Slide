using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GhostBlock3 : MonoBehaviour {
    //T블록 전용 1.5
    public GameObject cBlock;
    Vector3 maxY;
    Block block;
    stage stage1;

    public float distanceY;
    RaycastHit hit;
    int layerMask;

    void Awake() {
        int layerMask = (-1) - (1 << LayerMask.NameToLayer("Block"));
    }

    void Start() {
        block = GameObject.FindWithTag("Block").GetComponent<Block>();
        stage1 = GameObject.Find("Stage").GetComponent<stage>();
    }


    void Update() {
        cBlock = block.target;


        if (cBlock != null) {
            if (Physics.Raycast(cBlock.transform.parent.position, Vector3.up * (-1), out hit, 20)) {
                if (hit.collider.gameObject.tag == "Floor" || hit.collider.gameObject.tag == "FixedBlock") {
                    distanceY = hit.distance - 1;
                    maxY = new Vector3(0, distanceY, 0);
                }
            }


            transform.parent.rotation = cBlock.transform.parent.rotation;
            transform.parent.position = cBlock.transform.parent.position - maxY;

        }
    }


    void OnCollisionEnter(Collision other) {
        if (other.gameObject.tag == "OnFixedBlock") {
            Destroy(this.gameObject);
        }
    }

    void OnTriggerStay(Collider other) {
        if (other.gameObject.tag == "OnFixedBlock") {
            Destroy(this.gameObject);

        }
    }








}
