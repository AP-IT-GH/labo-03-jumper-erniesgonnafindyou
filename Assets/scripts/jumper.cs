using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.InputSystem;

public class jumper : Agent 
{
    public Rigidbody rb;
    public GameObject obstacle;
    public GameObject bonus;
    
    [Header("Snappy Physics")]
    public float jumpVelocity = 15f; // Directe snelheid omhoog
    public float moveSpeed = 10f;    // Sneller bewegen op de Z-as
    public float fallMultiplier = 8f; // KNAL naar beneden

    private float obstacleSpeed;
    private bool isGrounded;
    private int direction = 1;

    public override void OnEpisodeBegin()
    {
        transform.localPosition = new Vector3(0, 0.51f, 0);
        rb.linearVelocity = Vector3.zero;
        
        direction = (Random.value > 0.5f) ? 1 : -1;
        float startX = (direction == 1) ? -10f : 10f;
        obstacle.transform.localPosition = new Vector3(startX, 0.5f, 0);
        
        // Bonus op een random Z-positie
        float randomZ = Random.Range(-4f, 4f);
        bonus.transform.localPosition = new Vector3(startX, 2.5f, randomZ);
        bonus.SetActive(true);

        // Pas de snelheid aan: iets lager zodat je tijd hebt om te sturen!
        obstacleSpeed = Random.Range(6f, 10f);
    }

    private void FixedUpdate()
    {
        // 1. Betrouwbare Ground Check met een korte Raycast omlaag
        // We schieten een straal van 0.6f omlaag (Agent is 1 unit hoog, dus 0.5 is de bodem)
        isGrounded = Physics.Raycast(transform.position, Vector3.down, 0.6f);

        // Debug lijn om in de Scene view te zien of de raycast de grond raakt
        Debug.DrawRay(transform.position, Vector3.down * 0.6f, isGrounded ? Color.green : Color.red);

        // 2. Extra zwaartekracht voor dat snappy gevoel
        rb.AddForce(Vector3.down * fallMultiplier * 10f, ForceMode.Acceleration);
    }

    // Je kunt de CollisionEnter nu simpeler houden of zelfs weglaten voor isGrounded
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            SetReward(-1.0f);
            EndEpisode(); 
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // 1. SPRINGEN: Directe velocity override voor instant reactie
        if (actions.DiscreteActions[0] == 1)
        {
            // De straf gebeurt nu bij ELKE poging tot springen (elke frame dat de actie 1 is)
            AddReward(-0.05f);

            // De feitelijke fysieke sprong gebeurt alleen als de agent op de grond staat
            if (isGrounded)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpVelocity, rb.linearVelocity.z);
                isGrounded = false;
            }
        }

        // 2. Z-AS BEWEGING: Sneller maken
        float zInput = 0f;
        if (actions.DiscreteActions[1] == 1) zInput = 1f;
        else if (actions.DiscreteActions[1] == 2) zInput = -1f;

        float zDist = Mathf.Abs(bonus.transform.localPosition.z - transform.localPosition.z);
        if (zDist < 0.5f) 
        {
            AddReward(0.005f); // Beloning voor 'on target' zijn
        }
        
        // Gebruik Rigidbody voor verplaatsing zodat het matcht met de physics
        Vector3 move = new Vector3(0, 0, zInput * moveSpeed);
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, rb.linearVelocity.y, move.z);

        // Beweeg de balk
        float moveStep = direction * obstacleSpeed * Time.deltaTime;
        obstacle.transform.localPosition += new Vector3(moveStep, 0, 0);
        
        Vector3 bPos = bonus.transform.localPosition;
        bPos.x = obstacle.transform.localPosition.x;
        bonus.transform.localPosition = bPos;

        // Checks (Dood/Succes)
        CheckStatus();
    }

    private void CheckStatus()
    {
        if ((direction == 1 && obstacle.transform.localPosition.x > 11f) || 
            (direction == -1 && obstacle.transform.localPosition.x < -11f))
        {
            AddReward(0.8f);
            EndEpisode();
        }

        if (transform.localPosition.y < -0.5f || Mathf.Abs(transform.localPosition.z) > 5.5f)
        {
            SetReward(-1.0f);
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        if (Keyboard.current != null)
        {
            discreteActions[0] = Keyboard.current.spaceKey.isPressed ? 1 : 0;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) discreteActions[1] = 1;
            else if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) discreteActions[1] = 2;
            else discreteActions[1] = 0;
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 1. Relatieve afstand tot balk (X-as)
        float distToObstacle = obstacle.transform.localPosition.x - transform.localPosition.x;
        sensor.AddObservation(distToObstacle);

        // 2. Relatieve afstand tot bonus (Z-as) -> DIT IS DE KEY!
        // Hiermee voelt hij direct of hij naar links of rechts (vooruit/achteruit) moet
        float distToBonusZ = bonus.transform.localPosition.z - transform.localPosition.z;
        sensor.AddObservation(distToBonusZ);

        // 3. Snelheid en richting van de balk
        sensor.AddObservation(obstacleSpeed * direction);

        // 4. Ben ik op de grond?
        sensor.AddObservation(isGrounded ? 1f : 0f);
        
        // TOTAAL: 4 observations (Zorg dat Space Size in Inspector op 4 staat!)
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Bonus")) { AddReward(1.5f); other.gameObject.SetActive(false); }
    }
}