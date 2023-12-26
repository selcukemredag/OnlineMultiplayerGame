using System.Collections;
using UnityEngine;
using Photon.Pun;

public class parct : MonoBehaviour
{
    public int firepower;
    public string sourcePlayerId;
    private bool hasCollided = false;
    [SerializeField]
    private Player MyPlayer;
    public Player TargetPlayer;

    // Start is called before the first frame update
    void Start()
    {
        MyPlayer = gameObject.GetComponentInParent<Player>();
        StartCoroutine(DestroyObject(2f));
        //Destroy(gameObject, 2f);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    IEnumerator DestroyObject(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        PhotonNetwork.Destroy(gameObject);
    }

    private void OnParticleCollision(GameObject other)
    {
        // Check if the particle has already collided to prevent multiple collision handling.
        if (hasCollided) return;

        // Check if the collided object is not null and if it's tagged as a "Player".
        if (other != null && other.gameObject.CompareTag("Player"))
        {
            // Get the Player component of the collided object.
            var player = other.gameObject.GetComponent<Player>();

            // Ensure the collided player is not the one who fired the particle.
            if (player.UserID_Player != sourcePlayerId)
            {
                // Get the PhotonView component of the collided player.
                PhotonView targetPhotonView = other.gameObject.GetComponent<PhotonView>();

                // Mark that the particle has collided to prevent future collisions.
                hasCollided = true;

                // Ensure the PhotonView component is not null.
                if (targetPhotonView != null)
                {
                    // Assign the collided player as the target player.
                    TargetPlayer = player;

                    // Calculate the target player's health after applying the damage.
                    int projectedHealth = TargetPlayer.getHealth() - firepower;

                    // Use Photon's RPC (Remote Procedure Call) to apply damage to the target player across the network.
                    targetPhotonView.RpcSecure("ApplyDamage", RpcTarget.AllBuffered, true, firepower, sourcePlayerId);

                    // Get the Renderer component of the particle and disable it, effectively hiding the particle.
                    var renderer = GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.enabled = false;
                    }

                    // Check if the target player's health is zero or below, and the current player is not the one who fired.
                    if (PhotonNetwork.LocalPlayer.UserId != sourcePlayerId && projectedHealth <= 0)
                    {
                        // Increment the score of the player who fired the particle.
                        MyPlayer.IncrementScore();
                    }

                    // Start a coroutine to destroy the particle after a short delay.
                    StartCoroutine(DestroyAfterDelay(0.5f)); // Delay of 0.5 seconds
                }
            }
        }
    }

    IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        PhotonNetwork.Destroy(gameObject);
    }

}
