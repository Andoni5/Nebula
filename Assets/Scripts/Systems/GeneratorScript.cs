using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GeneratorScript : MonoBehaviour
{
    // === SERIALIZED FIELDS ===
    public GameObject[] availableRooms;
    public List<GameObject> currentRooms;
    public GameObject[] availableObjects;
    public List<GameObject> objects;
    public float objectsMinDistance = 5.0f;
    public float objectsMaxDistance = 10.0f;
    public float objectsMinY = -1.4f;
    public float objectsMaxY = 1.4f;
    public float objectsMinRotation = -45.0f;
    public float objectsMaxRotation = 45.0f;

    // === PRIVATE FIELDS ===
    private float screenWidthInPoints;

    // === UNITY CALLBACKS ===
    private void Start()
    {
        // Loads room and object prefabs, then starts the generation coroutine.
        availableRooms   = Resources.LoadAll<GameObject>("Rooms");
        availableObjects = Resources.LoadAll<GameObject>("Objects");

        currentRooms = new List<GameObject>();
        objects      = new List<GameObject>();

        float height = 2f * Camera.main.orthographicSize;
        screenWidthInPoints = height * Camera.main.aspect;

        StartCoroutine(GeneratorCheck());
    }

    private void Update()
    {
        // Unused—generation is handled in the coroutine.
    }

    // === COROUTINES ===
    private IEnumerator GeneratorCheck()
    {
        // Periodically spawns and removes rooms and objects as needed.
        while (true)
        {
            GenerateRoomIfRequired();
            GenerateObjectsIfRequired();
            yield return new WaitForSeconds(0.25f);
        }
    }

    // === ROOM GENERATION METHODS ===
    private void AddRoom(float farthestRoomEndX)
    {
        // Creates a random room and positions it after the current farthest room.
        int randomRoomIndex = Random.Range(0, availableRooms.Length);
        GameObject room     = Instantiate(availableRooms[randomRoomIndex]);

        float roomWidth  = room.transform.Find("floor").localScale.x;
        float roomCenter = farthestRoomEndX + roomWidth * 0.5f;

        room.transform.position = new Vector3(roomCenter, 0f, 0f);
        currentRooms.Add(room);
    }

    private void GenerateRoomIfRequired()
    {
        // Determines if new rooms must be added or old rooms removed.
        List<GameObject> roomsToRemove = new List<GameObject>();
        bool addRooms                  = true;

        float playerX       = transform.position.x;
        float removeRoomX   = playerX - screenWidthInPoints;
        float addRoomX      = playerX + screenWidthInPoints;
        float farthestEndX  = 0f;

        foreach (GameObject room in currentRooms)
        {
            float roomWidth  = room.transform.Find("floor").localScale.x;
            float roomStartX = room.transform.position.x - roomWidth * 0.5f;
            float roomEndX   = roomStartX + roomWidth;

            if (roomStartX > addRoomX) addRooms = false;
            if (roomEndX   < removeRoomX) roomsToRemove.Add(room);

            farthestEndX = Mathf.Max(farthestEndX, roomEndX);
        }

        foreach (GameObject room in roomsToRemove)
        {
            currentRooms.Remove(room);
            Destroy(room);
        }

        if (addRooms) AddRoom(farthestEndX);
    }

    // === OBJECT GENERATION METHODS ===
    private void AddObject(float lastObjectX)
    {
        // Instantiates a random obstacle after the last spawned object.
        int randomIndex = Random.Range(0, availableObjects.Length);
        GameObject obj  = Instantiate(availableObjects[randomIndex]);

        float posX = lastObjectX + Random.Range(objectsMinDistance, objectsMaxDistance);
        float posY = Random.Range(objectsMinY, objectsMaxY);
        obj.transform.position = new Vector3(posX, posY, 0f);

        float rotation = Random.Range(objectsMinRotation, objectsMaxRotation);
        obj.transform.rotation = Quaternion.Euler(Vector3.forward * rotation);

        objects.Add(obj);
    }

    private void GenerateObjectsIfRequired()
    {
        // Spawns new objects ahead of the player and removes ones left behind.
        float playerX         = transform.position.x;
        float removeObjectsX  = playerX - screenWidthInPoints;
        float addObjectX      = playerX + screenWidthInPoints;
        float farthestObjectX = 0f;

        for (int i = objects.Count - 1; i >= 0; i--)
        {
            GameObject obj = objects[i];

            if (obj == null)
            {
                objects.RemoveAt(i);
                continue;
            }

            float objX = obj.transform.position.x;
            farthestObjectX = Mathf.Max(farthestObjectX, objX);

            if (objX < removeObjectsX)
            {
                Destroy(obj);
                objects.RemoveAt(i);
            }
        }

        if (farthestObjectX < addObjectX) AddObject(farthestObjectX);
    }
}