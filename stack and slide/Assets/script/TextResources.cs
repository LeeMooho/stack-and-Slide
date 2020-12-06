using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.IO;

public class TextResources : MonoBehaviour
{


    //public List<Spawn> spawnList;
    //public int spawnIndex;
    //public bool spawnEnd;

    //void Awake() {
    //    spawnList = new List<Spawn>;
    //    ReadSpawnFile()

    //}

    //void ReadSpawnFile() {
    //    //변수 초기화
    //    spwnList.Clear();
    //    spawnIndex = 0;
    //    spawnEnd = false;

    //    // 리스폰 파일 읽기
    //    TextAsset textFile = Resources.Load("stage 0") as TextAsset;
    //    StringReader stringReader = new StringReader(textFile.text);

    //    while (stringReader != null) {

    //        string line = stringReader.ReadLine();
    //        Debug.Log(line);
    //        if (line == null)
    //            break;

    //        //리스폰 데이터 생성
    //        string line = stringReader.ReadLine();
    //        Spawn spawnData = new Spawn();
    //        spawnData.delay = float.Parse(line.Split('/')[0]);
    //        spawnData.type = line.Split('/')[1];
    //        spawnData.point = int.Parse(line.Split('/')[2]);
    //        spawnList.Add(spawnData);

    //    }

    //    //텍스트 파일 닫기
    //    stringReader.Close();
    //    // 첫번째 스폰 딜레이 적용 퍼블릭에 들어갈 것
    //    nextSpawnDelay = spawnList[0].delay;
    //}

    //void Function() {
    //    //다른 함수에 들어갈 구조체를 활용한 로직
    //    int enemyIndex = 0;
    //    switch (spawnList[spawnIndex].type) {
    //        case "S":
    //            enemyIndex = 0;
    //            break;
    //        case "M":
    //            enemyIndex = 1;
    //            break;
    //        case "L":
    //            enemyIndex = 2;
    //            break;
    //    }
    //    // 기존 로직
    //    //리스폰 인덱스 증가
    //    spawnIndex++;
    //    if (spawnIndex == spawnList.Count) {
    //        spawnEnd = true;
    //        return;
    //    }
    //    //다음 리스폰 딜레이 갱신
    //    nextSpawnDelay = spawnList[spawnIndex].delay;
    //    //spawnEnd 는 함수 조건넣어 확인 

    //}





}
