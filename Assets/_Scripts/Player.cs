using System.Collections;
using UnityEngine;
using Photon.Pun;
using TMPro;
using Firebase.Database;
using UnityEngine.UI;
using PimDeWitte.UnityMainThreadDispatcher;
using Photon.Pun.Demo.PunBasics;
public class Player : MonoBehaviour
{
    private PhotonView pw;
    private Animator Anim;

    public TextMeshProUGUI userName;
    public TextMeshProUGUI healthText;

    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI highScoreText;

    public Slider HealthBar;

    public GameObject Canvas;

    public string UserID_Player;
    public EncryptionForData encryptor_P;
    public GameObject FirePoint;
    private GameObject effect;
    public DatabaseReference dbReference;
    public Button RessurrectButton;
    public Button LogOutButton;

    private int playerFirePower;
    private int Health;

    private int currentScore = 0;
    private int highScore = 0;

    void Start()
    {
        pw = GetComponent<PhotonView>();
        Anim = GetComponent<Animator>();
        dbReference = FirebaseDatabase.DefaultInstance.RootReference;
        if (pw.IsMine)
        {
            PhotonNetwork.LocalPlayer.TagObject = this;
            InitializePlayerScore();
            InitializeAndFetchHighScore();
            UpdateScoreDisplay();
        }
        HealthBar.value = Health;
        userName.text = pw.Owner.NickName;
        PlayerDataInitializer();
        
        dbReference.Child("users").Child(UserID_Player).Child("health").ValueChanged += HandleHealthChanged;


        Debug.Log("GameStart Player ID: " + UserID_Player);

        CameraWork _cameraWork = gameObject.GetComponent<CameraWork>();

        if (_cameraWork != null)
        {
            if (pw.IsMine)
            {
                _cameraWork.OnStartFollowing();
            }
        }
        else
        {
            Debug.LogError("<Color=Red><b>Tanımsız</b></Color> Kamera eklenemedi", this);
        }
    }
    

    // Update is called once per frame
    void Update()
    {
        if (pw.IsMine)
        {
            Canvas.transform.rotation = Quaternion.Euler(90, 0, 0);
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {

                Vector3 dir = hit.point - transform.position;
                dir.y = 0;
                transform.rotation = Quaternion.LookRotation(dir * Time.deltaTime * 2f);
                Debug.DrawLine(transform.position, hit.point);

            }
            float x = Input.GetAxis("Horizontal") * Time.deltaTime * 20f;
            float y = Input.GetAxis("Vertical") * Time.deltaTime * 20f;
            transform.Translate(x, 0, y);

            FireWeapon();

            if (Input.GetKey(KeyCode.B))
            {
                Anim.SetBool("IsBig", true);
            }
            else
            {
                Anim.SetBool("IsBig", false);
            }

            if (Input.GetKeyDown(KeyCode.L))
            {
                IncrementScore();
            }
        }
    }

    void OnDestroy()
    {
        if (dbReference != null && UserID_Player != null)
        {
            // Unsubscribe from high score changes
            var highScoreRef = dbReference.Child("users").Child(UserID_Player).Child("highScore");
            highScoreRef.ValueChanged -= OnHighScoreChanged;

            // Unsubscribe from health changes
            var healthRef = dbReference.Child("users").Child(UserID_Player).Child("health");
            healthRef.ValueChanged -= HandleHealthChanged;
        }
    }

    public void PlayerDataInitializer()
    {
        UpdatePlayerHealth(100);
        UpdatePlayerFirepower(5);
    }

    void InitializePlayerScore()
    {
        // Assuming starting score is 0 or fetch from server if needed
        if (pw.IsMine)
        {
            currentScore = 0;
            scoreText.text = "Score: " + currentScore.ToString();
            UpdateScoreDisplay();
            UpdatePhotonCustomProperties();
        }
    }

    public int getHealth()
    {
        return Health;
    }

    public void FireWeapon()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            effect = PhotonNetwork.Instantiate("FireParct", FirePoint.transform.position, Quaternion.Euler(-90f, transform.eulerAngles.y, 0f));
            effect.GetComponent<parct>().firepower = playerFirePower;
            effect.GetComponent<parct>().sourcePlayerId = UserID_Player;
            effect.transform.SetParent(transform);
        }
    }

    [PunRPC]
    public void ApplyDamage(int damagePower, string sourcePlayerId)
    {
        // Check if the player receiving the damage is not the one who inflicted it.
        if (sourcePlayerId != UserID_Player)
        {
            // Calculate the new health of the player after receiving the damage.
            int newHealth = Health - damagePower;

            // Update the health of the player locally within this client.
            UpdateLocalHealth(newHealth);

            // Asynchronously update the player's health in Firebase, which could be used for persistent data tracking or multiplayer game state synchronization.
            UpdatePlayerHealth(newHealth);

            // Log the player's ID along with the updated health and firepower for debugging purposes.
            Debug.Log(UserID_Player + " Health: " + Health);
            Debug.Log(UserID_Player + " firePower: " + playerFirePower);

            // Check if the player's health has dropped to zero or below.
            if (newHealth <= 0)
            {
                // Log a message indicating the player's character has died.
                Debug.Log("Player has died");

                // Handle the player's death, which could include actions like removing the player from the game, updating the game state, or triggering a respawn mechanism.
                HandlePlayerDeath();
            }
        }
    }

    private void HandlePlayerDeath()
    {
        if (pw.IsMine)
        {
            currentScore = 0; // Reset score on death
            UpdatePhotonCustomProperties(); // Update properties with the reset score
        }

        PhotonNetwork.Destroy(gameObject);
        //Resurrect();
        // Additional death handling logic
    }

    public void UpdatePlayerHealth(int health)
    {
        string encryptedHealth = EncryptionForData.EncryptString(health.ToString());
        dbReference.Child("users").Child(UserID_Player).Child("health").SetValueAsync(encryptedHealth);
    }

    public void UpdatePlayerFirepower(int firePower)
    {
        string encryptedHealth = EncryptionForData.EncryptString(firePower.ToString());
        dbReference.Child("users").Child(UserID_Player).Child("firePower").SetValueAsync(encryptedHealth);
        RetrieveAndSetPlayerFirePower();
        Debug.Log("Updated Health ID: " + UserID_Player);
    }

    public void RetrieveAndSetPlayerFirePower()
    {
        dbReference.Child("users").Child(UserID_Player).Child("firePower").GetValueAsync().ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                // Handle the error...
            }
            else if (task.IsCompleted)
            {
                Debug.Log("Data Task Result = " + task.Result);
                DataSnapshot snapshot = task.Result;
                Debug.Log("Data snapshot = " + snapshot);
                string encryptedData = snapshot.Value.ToString();
                string decryptedData = EncryptionForData.DecryptString(encryptedData);
                playerFirePower = int.Parse(decryptedData);
            }
        });
    }

    private void HandleHealthChanged(object sender, ValueChangedEventArgs args)
    {
        // Check if there are any errors in the database operation.
        if (args.DatabaseError != null)
        {
            // If there is a database error, handle it here. 
            // This could involve logging the error, notifying the user, or attempting to retry the operation.
            return; // Exit the method early if there's an error.
        }

        // Use UnityMainThreadDispatcher to perform actions on the main thread.
        // This is necessary because Firebase's ValueChanged event might not be called on the main thread,
        // but Unity API calls (like updating UI elements) need to be done on the main thread.
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            // Extract the encrypted health data from the Firebase snapshot.
            // Firebase stores and returns the health data as an encrypted string for security.
            string encryptedHealth = args.Snapshot.Value.ToString();

            // Decrypt the health data to convert it back to a usable integer format.
            // The encryption is used for data security, especially when sending data over the network.
            string decryptedHealth = EncryptionForData.DecryptString(encryptedHealth);

            // Parse the decrypted string into an integer representing the player's health.
            int health = int.Parse(decryptedHealth);

            // Update the player's health locally with the new value.
            // This method would typically update the health bar UI or any other in-game representation of health.
            UpdateLocalHealth(health);
        });
    }

    private void UpdateLocalHealth(int newHealth)
    {
        // Update the player's health with the new value.
        // The 'Health' variable stores the current health of the player within this script.
        Health = newHealth;

        // Update the value of the health bar UI to reflect the new health.
        // 'HealthBar' is likely a UI Slider component that visually represents the player's health.
        // Setting its value to the player's current health updates the UI accordingly.
        HealthBar.value = Health;

        // Here I can include additional UI updates or game logic related to the health change.
        // For example, I might change the color of the health bar if health is low,
        // show damage effects, or trigger certain animations or sound effects.
    }

    private void InitializeAndFetchHighScore()
    {
        // Setting up a listener for any changes in the 'highScore' value in the Firebase database for this player.
        // 'dbReference' is a DatabaseReference object pointing to the Firebase database root.
        // 'Child("users")' navigates to the 'users' node in the Firebase database.
        // 'Child(UserID_Player)' navigates further to a child node specific to this player, identified by 'UserID_Player'.
        // 'Child("highScore")' targets the 'highScore' field of this player in the database.

        // '+=' adds an event listener to the 'ValueChanged' event of the 'highScore' database reference.
        // 'OnHighScoreChanged' is a method that will be called whenever the 'highScore' value in the database changes.
        // This setup allows the game to react in real-time to changes in the player's high score stored in Firebase.
        dbReference.Child("users").Child(UserID_Player).Child("highScore").ValueChanged += OnHighScoreChanged;
    }

    public void IncrementScore()
    {
        // Check if the PhotonView (network component) is controlled by the local player.
        if (pw.IsMine)
        {
            // Increment the current score of the player by 5 points.
            currentScore += 5;

            // Encrypt the updated score using a custom encryption method.
            // This is likely for security purposes, to prevent tampering with the score data.
            string encryptedScore = EncryptionForData.EncryptString(currentScore.ToString());

            // Create a new Hashtable to hold updated player properties.
            ExitGames.Client.Photon.Hashtable newProperties = new ExitGames.Client.Photon.Hashtable();

            // Add the encrypted score to the Hashtable with a key "EncryptedScore".
            newProperties.Add("EncryptedScore", encryptedScore);

            // Update the local player's custom properties on the Photon network with the new encrypted score.
            // This is a way to synchronize the score across the network, ensuring all players see the updated score.
            PhotonNetwork.LocalPlayer.SetCustomProperties(newProperties);

            // Start a coroutine to defer the update of the score display and the high score check.
            // This might be necessary to ensure the properties are updated across the network before proceeding.
            StartCoroutine(UpdateScoreAfterPropertiesSet());
        }
    }

    private IEnumerator UpdateScoreAfterPropertiesSet()
    {
        // Wait for a moment to ensure properties are updated
        yield return new WaitForSeconds(0.1f);

        // Update score display
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("EncryptedScore", out object retrievedEncryptedScore))
        {
            string decryptedScore = EncryptionForData.DecryptString(retrievedEncryptedScore.ToString());
            scoreText.text = "Score: " + decryptedScore;
            currentScore = int.Parse(decryptedScore);  // Ensure the current score is in sync

            // Check and update high score if necessary
            CheckAndUpdateHighScore(currentScore);
            UpdateScoreDisplay();
        }
        else
        {
            Debug.LogError("Failed to retrieve encrypted score from custom properties.");
        }
    }


    public void CheckAndUpdateHighScore(int currentScore)
    {

        if (currentScore > highScore)
        {
            highScore = currentScore;
            string encryptedHighScore = EncryptionForData.EncryptString(highScore.ToString());
            dbReference.Child("users").Child(UserID_Player).Child("highScore").SetValueAsync(encryptedHighScore);
            UpdateHighScoreDisplay();
        }
    }

    private void UpdateScoreDisplay()
    {
        Debug.Log("Updating Score Display");
        if (pw.IsMine && PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("EncryptedScore", out object encryptedScore))
        {
            string decryptedScore = EncryptionForData.DecryptString(encryptedScore.ToString());
            currentScore = int.Parse(decryptedScore);
            Debug.Log("New Score: " + currentScore);
            scoreText.text = "Score: " + currentScore;
        }
        else
        {
            scoreText.text = "Score: 0";
        }
    }

    private void UpdateHighScoreDisplay()
    {
        highScoreText.text = "High Score: " + highScore;
        UpdatePhotonCustomProperties();
    }

    private void OnHighScoreChanged(object sender, ValueChangedEventArgs args)
    {
        // Check if there was an error when the high score value was changed in the database.
        if (args.DatabaseError != null)
        {
            // Log an error message with the details of the database error.
            Debug.LogError("Error fetching high score: " + args.DatabaseError.Message);
            return; // Exit the method if there was an error.
        }

        // Check if the high score data exists in the database snapshot.
        if (args.Snapshot.Exists)
        {
            // Retrieve the encrypted high score from the database snapshot.
            string encryptedHighScore = args.Snapshot.Value.ToString();

            // Decrypt the high score to obtain its integer value.
            highScore = int.Parse(EncryptionForData.DecryptString(encryptedHighScore));

            // Update the high score display on the UI with the new value.
            UpdateHighScoreDisplay();
        }
        else
        {
            // If the high score does not exist, initialize it to zero in an encrypted format.
            string encryptedHighScore = EncryptionForData.EncryptString("0");

            // Set the high score in the database for this player to zero.
            dbReference.Child("users").Child(UserID_Player).Child("highScore").SetValueAsync(encryptedHighScore);

            // Set the local high score variable to zero.
            highScore = 0;

            // Update the high score display on the UI with zero.
            UpdateHighScoreDisplay();
        }
    }

    public void UpdatePhotonCustomProperties()
    {
        if (pw.IsMine)
        {
            string encryptedScore = EncryptionForData.EncryptString(currentScore.ToString());
            string encryptedHighScore = EncryptionForData.EncryptString(highScore.ToString());

            ExitGames.Client.Photon.Hashtable properties = new ExitGames.Client.Photon.Hashtable
    {
        { "EncryptedScore", encryptedScore },
        { "EncryptedHighScore", encryptedHighScore }
    };
            PhotonNetwork.LocalPlayer.SetCustomProperties(properties);
        }
    }
}
