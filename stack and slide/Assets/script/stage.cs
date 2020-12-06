using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class stage : MonoBehaviour {
    public GameObject[] floor;
    public GameObject[] wall;
    public GameObject[] frontwall;
    public RectTransform[] Location;
    public GameObject playUI;
    public GameObject mainMenu;

    public GameObject A1Image;
    public GameObject A2Image;
    public GameObject IImage;
    public GameObject OImage;
    public GameObject PImage;
    public GameObject TImage;
    public GameObject ZImage;

    public GameObject A1Block;
    public GameObject A2Block;
    public GameObject IBlock;
    public GameObject OBlock;
    public GameObject PBlock;
    public GameObject TBlock;
    public GameObject ZBlock;
    public Vector3 Origin;
    public Vector3 GhostOrigin;

    public GameObject GhostA1Block;
    public GameObject GhostA2Block;
    public GameObject GhostIBlock;
    public GameObject GhostOBlock;
    public GameObject GhostPBlock;
    public GameObject GhostTBlock;
    public GameObject GhostZBlock;

    GameObject pre_Block;
    public GameObject pre_GhostBlock;

    public Text A1Text;
    public Text A2Text;
    public Text IText;
    public Text OText;
    public Text PText;
    public Text TText;
    public Text ZText;

    int A1Number;
    int A2Number;
    int INumber;
    int ONumber;
    int PNumber;
    int TNumber;
    int ZNumber;
    bool OnPlay;

    int[] stage_Floor;
    int[] stage_Wall;
    int[] stage_FrontWall;


    stage stage1;
    Block block;
    StageManager stageManager;
    MeshRenderer mesh;
    Material mat;


    private RaycastHit hit;
    private int maxDistance = 8;



    void Awake() {
        playUI.SetActive(false);
        mainMenu.SetActive(true);
        ResetStage();
    }


    void leftBlock() {
        for (int y = 0; y < 7; y++) {
            if (A1Number > 0) {
                A1Image.SetActive(true);
                A1Image.transform.position = Location[y].transform.position;
                A1Text.text = "" + A1Number;
                y++;
            }
            else if (A1Number == 0) {
                A1Image.SetActive(false);
            }
            if (A2Number > 0) {
                A2Image.SetActive(true);
                A2Image.transform.position = Location[y].transform.position;
                A2Text.text = "" + A2Number;
                y++;
            } else if (A2Number == 0) {
                A2Image.SetActive(false);
            }
            if (INumber > 0) {
                IImage.SetActive(true);
                IImage.transform.position = Location[y].transform.position;
                IText.text = "" + INumber;
                y++;
            } else if (INumber == 0) {
                IImage.SetActive(false);
            }
            if (ZNumber > 0) {
                ZImage.SetActive(true);
                ZImage.transform.position = Location[y].transform.position;
                ZText.text = "" + ZNumber;
                y++;
            } else if (ZNumber == 0) {
                ZImage.SetActive(false);
            }
            if (ONumber > 0) {
                OImage.SetActive(true);
                OImage.transform.position = Location[y].transform.position;
                OText.text = "" + ONumber;
                y++;
            } else if (ONumber == 0) {
                OImage.SetActive(false);
            }
            if (PNumber > 0) {
                PImage.SetActive(true);
                PImage.transform.position = Location[y].transform.position;
                PText.text = "" + PNumber;
                y++;
            } else if (PNumber == 0) {
                PImage.SetActive(false);
            }
            if (TNumber > 0) {
                TImage.SetActive(true);
                TImage.transform.position = Location[y].transform.position;
                TText.text = "" + TNumber;
                y++;
            } else if (TNumber == 0) {
                TImage.SetActive(false);
            }



            return;

        }



    }




    void Start() {
    }

    public void SelectStage(int A) {
        switch(A){
            case 101:
                StageManager.Stage101();
                playUI.SetActive(true);
                mainMenu.SetActive(false);
                OnPlay = true;
                FloorColor();
                break;

        }
    }




    void FloorColor() {

        while (OnPlay = true) {
            for (int i = 0; i < 64; i++) {
                if (stage_Floor[i] == 1) {
                    floor[i].GetComponent<MeshRenderer>().material.color = new Color(0, 0, 0);
                }

                if (stage_Wall[i] == 1) {
                    wall[i].GetComponent<MeshRenderer>().material.color = new Color(0, 0, 0);
                }

                if (stage_FrontWall[i] == 1) {
                    frontwall[i].GetComponent<MeshRenderer>().material.color = new Color(0, 0, 0);
                }
            }
        }
    }



    public void GenerateBlock(int A) {
        switch (A){
            case 1:
                Vector3 Adjust1 = new Vector3(0.5f,0.5f,0.5f);
                pre_GhostBlock = Instantiate(GhostA1Block, GhostOrigin + Adjust1, Quaternion.identity);
                pre_Block = Instantiate(A1Block, Origin+ Adjust1, Quaternion.identity);
                A1Number--;
                leftBlock();
                break;

            case 2:
                Vector3 Adjust2 = new Vector3(0.5f, 0.5f, 0.5f);
                pre_GhostBlock = Instantiate(GhostA2Block, GhostOrigin + Adjust2, Quaternion.identity);
                pre_Block = Instantiate(A2Block, Origin+ Adjust2, Quaternion.identity);
                A2Number--;
                leftBlock();
                break;

            case 3:
                Vector3 Adjust3 = new Vector3(0, 0, 0.5f);
                pre_GhostBlock = Instantiate(GhostIBlock, GhostOrigin + Adjust3, Quaternion.identity);
                pre_Block = Instantiate(IBlock, Origin+ Adjust3, Quaternion.identity);
                INumber--;
                leftBlock();
                break;

            case 4:
                Vector3 Adjust4 = new Vector3(0.5f, 0, 0);
                pre_GhostBlock = Instantiate(GhostOBlock, GhostOrigin + Adjust4, Quaternion.identity);
                pre_Block = Instantiate(OBlock, Origin+ Adjust4, Quaternion.identity);
                ONumber--;
                leftBlock();
                break;

            case 5:
                Vector3 Adjust5 = new Vector3(0, 0.5f, 0.5f);
                pre_GhostBlock = Instantiate(GhostPBlock, GhostOrigin + Adjust5, Quaternion.identity);
                pre_Block = Instantiate(PBlock, Origin+ Adjust5, Quaternion.identity);
                PNumber--;
                leftBlock();
                break;

            case 6:
                Vector3 Adjust6 = new Vector3(0, 0.5f, 0);
                pre_GhostBlock = Instantiate(GhostTBlock, GhostOrigin + Adjust6, Quaternion.identity);
                pre_Block = Instantiate(TBlock, Origin+ Adjust6, Quaternion.identity);
                TNumber--;
                leftBlock();
                break;

            case 7:
                Vector3 Adjust7 = new Vector3(0, 0.5f, 0);
                pre_GhostBlock = Instantiate(GhostZBlock, GhostOrigin + Adjust7, Quaternion.identity);
                pre_Block = Instantiate(ZBlock, Origin+ Adjust7, Quaternion.identity);
                ZNumber--;
                leftBlock();
                break;
        }
    }


   
      


    public void Slide() {
        int WallSlide=0;
        int FrontSlide=0;
        int FloorSlide=0;

        for (int i = 0; i < 64; i++) {

            if (stage_Floor[i] == 1) {

                if (Physics.Raycast(floor[i].GetComponent<Transform>().position, GetComponent<Transform>().up, out hit, maxDistance)) {
                    if (hit.collider.gameObject.tag == "Block")
                        Debug.Log("바닥 있어야 할 것이 있음");
                    else {
                        Debug.Log("바닥 있어야 할 것이 없음");
                        FloorSlide++;
                            }
                }
            } 
            else {
                if (Physics.Raycast(floor[i].GetComponent<Transform>().position, GetComponent<Transform>().up,  maxDistance)) {

                    if (hit.collider.gameObject) {
                        Debug.Log("바닥 없어야 할 것이 있음");
                        FloorSlide++;
                    } else
                        Debug.Log("바닥 없어야 할 것이 없음");
                }

            }
        }




        for (int t = 0; t < 64; t++) {

            Debug.DrawRay(wall[t].GetComponent<Transform>().position, GetComponent<Transform>().forward  * maxDistance, Color.red, 20, false);

            if (stage_Wall[t] == 1) {

                if (Physics.Raycast(wall[t].GetComponent<Transform>().position, GetComponent<Transform>().up * (-1), out hit, maxDistance)) {
                    if (hit.collider.gameObject.tag == "Block")
                        Debug.Log("벽 있어야 할 것이 있음");
                    else {
                        WallSlide++;
                        Debug.Log("벽 있어야 할 것이 없음");
                    }

                }
            } else {
                if (Physics.Raycast(wall[t].GetComponent<Transform>().position, GetComponent<Transform>().up * (-1), out hit, maxDistance)) {

                    if (hit.collider.gameObject.tag == "Block") {
                        Debug.Log("벽 없어야 할 것이 있음");
                        WallSlide++;
                    } else

                        Debug.Log("벽 없어야 할 것이 없음");
                }
            }


        }
    

    
        for (int j = 0; j< 64; j++) {


            if (stage_FrontWall[j] == 1) {

                if (Physics.Raycast(frontwall[j].GetComponent<Transform>().position, GetComponent<Transform>().up* (-1), out hit, maxDistance)) {
                    if (hit.collider.gameObject.tag == "Block")
                        Debug.Log("앞벽 있어야 할 것이 있음");


                    else {
                        Debug.Log("앞벽 있어야 할 것이 없음");
                        FrontSlide++;
                    }

                }
            } 
            else {
                if (Physics.Raycast(frontwall[j].GetComponent<Transform>().position, GetComponent<Transform>().up * (-1), out hit, maxDistance)) {

                    if (hit.collider.gameObject.tag == "Block") {
                        FrontSlide++;
                        Debug.Log("앞벽 없어야 할 것이 있음");
                    }



                    else
                        Debug.Log("앞벽 없어야 할 것이 없음");
                }
            }


        }



        if (WallSlide + FrontSlide + FloorSlide > 0) {
            Debug.Log("실패");
        }

        else if(WallSlide + FrontSlide + FloorSlide == 0) {

            Debug.Log("성공");

        }




    }



    public void openMainMenu() {
        playUI.SetActive(false);
        mainMenu.SetActive(true);
    }

    void ResetStage() {

        A1Number = 0;
        A2Number = 0;
        INumber = 0;
        ONumber = 0;
        PNumber = 0;
        TNumber = 0;
        ZNumber = 0;

        stage_Floor = new int[64]{
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        };

        stage_Wall = new int[64]{
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        };


        stage_FrontWall = new int[64]{
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        };

    }


}


