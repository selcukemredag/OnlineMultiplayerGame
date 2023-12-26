using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using UnityEngine;
using System.Collections;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using PimDeWitte.UnityMainThreadDispatcher;
using Firebase.Database;

public class NetworkManager : MonoBehaviourPunCallbacks//, IConnectionCallbacks
{

    public TMP_InputField emailInput;
    public TMP_InputField passwordInput;

    public TMP_InputField emailRegisterInput;
    public TMP_InputField emailConfirmInput;
    public TMP_InputField passwordRegisterInput;
    public TMP_InputField passwordConfirmInput;
    public TMP_InputField usernameRegisterInput;

    public TMP_InputField resetPasswordEmailInput;

    public TextMeshProUGUI messageTextLogin;
    public TextMeshProUGUI messageTextRegister;
    public TextMeshProUGUI messageTextResetPassword;

    public GameObject MainMenu;
    public GameObject LoginMenu;
    public GameObject RegisterMenu;
    public GameObject ResetPassword_Menu;
    public GameObject LoginPanel;
    public GameObject PlayerList;
    public GameObject inGameButtons;

    private FirebaseAuth auth;
    public DatabaseReference dbReference;
    private PhotonView pw;

    private string UserID_E;
    private bool isInGame = false;

    public EncryptionForLogin encryptor;
    //private const string encryptionKey = "InnocentChukwuma";
    private bool shouldJoinRoomAfterLobby = true;

    // Start is called before the first frame update
    private void Start()
    {
        // Initialize Firebase
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            FirebaseApp app = FirebaseApp.DefaultInstance;
            Debug.Log("Databse: " + FirebaseDatabase.DefaultInstance.RootReference);

            if (task.Result == DependencyStatus.Available)
            {
                // Firebase is ready for use.
                auth = FirebaseAuth.DefaultInstance;
                dbReference = FirebaseDatabase.DefaultInstance.RootReference;
            }
            else
            {
                Debug.LogError("Firebase initialization failed.");
            }
        });
    }

    // Update is called once per frame
    void Update()
    {
        GetPlayerListAndScores();
    }

    private void DisplayPlayerListWithScores()
    {
        if (isInGame) 
        {
            PlayerList.SetActive(true);
            inGameButtons.SetActive(true);
            PlayerList.transform.Find("PlayerListText").GetComponent<TextMeshProUGUI>().text = "";

            foreach (var player in PhotonNetwork.PlayerList)
            {
                string scoreText = "Score: N/A";
                string highScoreText = "HighScore: N/A";

                if (player.CustomProperties.TryGetValue("EncryptedScore", out object encryptedScore))
                {
                    scoreText = "Score: " + EncryptionForData.DecryptString(encryptedScore.ToString());
                }
                if (player.CustomProperties.TryGetValue("EncryptedHighScore", out object encryptedHighScore))
                {
                    highScoreText = "HighScore: " + EncryptionForData.DecryptString(encryptedHighScore.ToString());
                }

                string playerInfo = player.NickName + " - " + scoreText + " " + highScoreText;
                if (player.IsMasterClient)
                {
                    playerInfo += " Master";
                }
                PlayerList.transform.Find("PlayerListText").GetComponent<TextMeshProUGUI>().text += playerInfo + "\n";
            }
        }
        
    }

    public void Register()
    {
        // Retrieve user input from registration fields
        string email = emailRegisterInput.text;
        string confirmEmail = emailConfirmInput.text;
        string password = passwordRegisterInput.text;
        string confirmPassword = passwordConfirmInput.text;
        string username = usernameRegisterInput.text;

        // Check if the provided email and confirmation email match
        if (email != confirmEmail)
        {
            messageTextRegister.text = "Email addresses do not match.";
            return; // Exit the method if emails do not match
        }
        // Check if the provided password and confirmation password match
        if (password != confirmPassword)
        {
            messageTextRegister.text = "Passwords do not match.";
            return; // Exit the method if passwords do not match
        }

        // Encrypt the username using a custom encryption method if the encryptor is available
        string encryptedUsername = encryptor != null ? EncryptionForLogin.Encrypt(username, encryptor.GetEncryptionKey()) : username;

        // Check if the encrypted username is valid
        if (string.IsNullOrEmpty(encryptedUsername))
        {
            Debug.LogError("Encrypted username is null or empty.");
            messageTextRegister.text = "Error: Invalid username.";
            return; // Exit the method if the username is invalid
        }

        // Attempt to create a new user with the provided email and password using Firebase
        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWith(task =>
        {
            if (task.IsCanceled)
            {
                Debug.LogError("CreateUserWithEmailAndPasswordAsync was canceled.");
                UpdateMessageTextRegister("Registration was canceled.");
                return; // Handle cancellation
            }
            if (task.IsFaulted)
            {
                Debug.LogError("CreateUserWithEmailAndPasswordAsync encountered an error: " + task.Exception);
                UpdateMessageTextRegister("Error: Registration failed.");
                return; // Handle errors during registration
            }

            // Handle successful user creation
            FirebaseUser user = task.Result.User;
            // Initialize the user's high score to zero in an encrypted format
            string encryptedHighScore = EncryptionForData.EncryptString("0");
            dbReference.Child("users").Child(user.UserId).Child("highScore").SetValueAsync(encryptedHighScore);

            // Send a verification email to the new user
            user.SendEmailVerificationAsync().ContinueWith(verifyTask =>
            {
                if (verifyTask.IsFaulted || verifyTask.IsCanceled)
                {
                    Debug.LogError("SendEmailVerificationAsync encountered an error: " + verifyTask.Exception);
                    return; // Handle errors in sending the verification email
                }

                // Store the encrypted username in Firebase
                dbReference.Child("users").Child(user.UserId).Child("username").SetValueAsync(encryptedUsername);
                Debug.Log("Registered and sent verification email: " + user.Email);

                // Notify the user of successful registration and send them back to the main menu
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    messageTextRegister.text = "Registration successful! Verification email sent.";
                    StartCoroutine(DelayedMenuSwitch(1.5f, "ReturnMenu")); // Switch to the main menu after a short delay
                });
            });
        });
    }

    public void ResetPassword()
    {
        // Retrieve the email input by the user for password reset
        string email = resetPasswordEmailInput.text;

        // Check if the email field is empty or null
        if (string.IsNullOrEmpty(email))
        {
            // Update the message text to prompt the user to enter an email address
            messageTextResetPassword.text = "Please enter your email address.";
            return; // Exit the method if no email is entered
        }

        // Attempt to send a password reset email using Firebase Authentication
        auth.SendPasswordResetEmailAsync(email).ContinueWith(task =>
        {
            // Execute the following in Unity's main thread
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                if (task.IsCanceled)
                {
                    // Log and display a message if the password reset process was canceled
                    Debug.LogError("SendPasswordResetEmailAsync was canceled.");
                    messageTextResetPassword.text = "Password reset was canceled.";
                    return;
                }
                if (task.IsFaulted)
                {
                    // Log and display an error message if the password reset process encountered an error
                    Debug.LogError("SendPasswordResetEmailAsync encountered an error: " + task.Exception);
                    messageTextResetPassword.text = "Error: Could not send password reset email.";
                    return;
                }

                // If the process is successful, display a confirmation message
                messageTextResetPassword.text = "Password reset email sent successfully.";
                // After a delay of 1.5 seconds, switch to a different menu (like the main menu)
                StartCoroutine(DelayedMenuSwitch(1.5f, "ReturnMenu"));
            });
        });
    }

    public void LoginWithEmailPassword()
    {
        // Retrieve the email and password input by the user
        string email = emailInput.text;
        string password = passwordInput.text;

        // Attempt to sign in with the provided email and password using Firebase
        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWith(task =>
        {
            if (task.IsCanceled)
            {
                Debug.LogError("Login canceled.");
                UpdateMessageTextLogin("Login canceled."); // Update login message if canceled
                return;
            }
            if (task.IsFaulted)
            {
                Debug.LogError("Login error: " + task.Exception);
                UpdateMessageTextLogin("Login failed. Please check your email and password."); // Update login message on error
                return;
            }

            // Handle successful login
            FirebaseUser user = task.Result.User;
            if (!user.IsEmailVerified)
            {
                // If the user's email is not verified, prevent login
                UpdateMessageTextLogin("Login failed. Please verify your email first.");
                return;
            }

            // Check if the user's high score is already set in Firebase
            dbReference.Child("users").Child(user.UserId).Child("highScore").GetValueAsync().ContinueWithOnMainThread(scoreTask =>
            {
                if (scoreTask.IsFaulted || scoreTask.IsCanceled)
                {
                    Debug.LogError("Error fetching high score."); // Log error if fetching high score fails
                    return;
                }

                if (!scoreTask.Result.Exists)
                {
                    // If high score does not exist, initialize it to zero
                    string encryptedHighScore = EncryptionForData.EncryptString("0");
                    dbReference.Child("users").Child(user.UserId).Child("highScore").SetValueAsync(encryptedHighScore);
                }

                // If email is verified and high score is set, login is successful
                UpdateMessageTextLogin("Login successful!");

                // Retrieve the username from Firebase
                DatabaseReference userRef = FirebaseDatabase.DefaultInstance.GetReference("users").Child(user.UserId);
                userRef.Child("username").GetValueAsync().ContinueWithOnMainThread(usernameTask =>
                {
                    if (usernameTask.IsFaulted || usernameTask.IsCanceled)
                    {
                        Debug.LogError("Could not retrieve username."); // Log error if username retrieval fails
                        UpdateMessageTextLogin("Error: Could not retrieve username.");
                        return;
                    }

                    DataSnapshot snapshot = usernameTask.Result;
                    string encryptedUsername = snapshot.Value.ToString();
                    string decryptedUsername = encryptor != null ? EncryptionForLogin.Decrypt(encryptedUsername, encryptor.GetEncryptionKey()) : encryptedUsername;

                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        if (encryptor != null)
                        {
                            // Encrypt the Firebase user ID and use it for Photon authentication
                            string encryptedUserID = EncryptionForLogin.Encrypt(user.UserId, encryptor.GetEncryptionKey());
                            LoginWithPhoton(encryptedUserID, decryptedUsername);
                            UserID_E = user.UserId; // Store the user ID
                        }
                        else
                        {
                            Debug.LogError("Encryptor is null. Ensure it's initialized correctly.");
                            UpdateMessageTextLogin("Error: Encryptor is not initialized."); // Log error if encryptor is not initialized
                        }
                    });
                });
            });
        });
    }

    IEnumerator DelayedMenuSwitch(float delayInSeconds, string methodName)
    {
        yield return new WaitForSeconds(delayInSeconds);

        // Call the method by name
        Invoke(methodName, 0f);
    }

    public void GetPlayerListAndScores()
    {
        if (Input.GetKey(KeyCode.Tab))
        {
            DisplayPlayerListWithScores();
        }
        else
        {
            PlayerList.SetActive(false);
        }
    }

    public void Resurrect()
    {
        if (pw.IsMine)
        {
            //shouldJoinRoomAfterLobby = false;
            PhotonNetwork.LeaveRoom();
        }
    }

    private void UpdateMessageTextLogin(string message)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            messageTextLogin.text = message;
        });
    }

    private void UpdateMessageTextRegister(string message)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            messageTextRegister.text = message;
        });
    }

    private void LoginWithPhoton(string encryptedUserID, string decryptedUsername)
    {
        Debug.Log("Methoda Girdi");
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.AuthValues = new AuthenticationValues();
            PhotonNetwork.AuthValues.UserId = encryptedUserID; // Use encrypted Firebase User ID as Photon UserID 
            //Note: We can also use decrypted User ID here, but decrypting is not necesseray for User ID.
            PhotonNetwork.NickName = decryptedUsername; // Setting the Photon nickname to the decrypted username
            PhotonNetwork.AuthValues.AuthType = CustomAuthenticationType.None;
            // We're not using Photon's built-in custom authentication types; we're simply using Firebase's UserID.

            shouldJoinRoomAfterLobby = true; 
            PhotonNetwork.GameVersion = "1.0"; // Ensure you have a consistent game version; you can change this as needed.
            PhotonNetwork.ConnectUsingSettings(); // Connect to Photon
            Debug.Log("baglanmis olmali");
            //LoginPanel.SetActive(false);
        }
    }

    public void ShowEmailPasswordFields()
    {
        MainMenu.SetActive(false);
        RegisterMenu.SetActive(false);
        LoginMenu.SetActive(true);
        ResetPassword_Menu.SetActive(false);
    }

    public void ShowRegisterMenu()
    {
        MainMenu.SetActive(false);
        RegisterMenu.SetActive(true);
        LoginMenu.SetActive(false);
        ResetPassword_Menu.SetActive(false);
    }

    public void ReturnMenu()
    {
        MainMenu.SetActive(true);
        RegisterMenu.SetActive(false);
        LoginMenu.SetActive(false);
        ResetPassword_Menu.SetActive(false);
    }

    public void ResetPasswordMenu()
    {
        MainMenu.SetActive(false);
        RegisterMenu.SetActive(false);
        LoginMenu.SetActive(false);
        ResetPassword_Menu.SetActive(true);
    }

    public void ReturnToMenu()
    {
        if (pw.IsMine)
        {
            StartCoroutine(HandleLogoutSequence());
        }
    }

    private IEnumerator HandleLogoutSequence()
    {

        // Disconnect from Photon
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }
        // Disconnect from Photon
        while (PhotonNetwork.IsConnected)
        {
            yield return null; // Wait until the disconnection is complete
        }

        // Sign out from Firebase
        if (FirebaseAuth.DefaultInstance.CurrentUser != null)
        {
            FirebaseAuth.DefaultInstance.SignOut();
        }

        // Load the login screen scene
        LoginPanel.SetActive(true);
        isInGame = false;
        if (!isInGame)
        {
            inGameButtons.gameObject.SetActive(false);

        }
        ReturnMenu();
        // Optionally load a new scene if needed
        // SceneManager.LoadScene("LoginScreen");
    }

    public override void OnConnected()
    {
        Debug.Log("Connected to Photon Server.");
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Photon Master Server.");

        OnJoinedLobby();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        base.OnDisconnected(cause);
        Debug.Log("Disconnected from Photon. Cause: " + cause);
        //Debug.Log($"Disconnected from Photon: {cause}");
        //PhotonNetwork.ReconnectAndRejoin();
    }

    public override void OnJoinedLobby()
    {
        // This method is called when the player successfully joins a Photon lobby.
        Debug.Log("Connected to Lobby");

        // Checks if the player should join a room after joining the lobby.
        if (shouldJoinRoomAfterLobby)
        {
            // Join an existing room with the given name or create one if it doesn't exist.
            // The room has a maximum of 5 players, and it's both open and visible to others.
            PhotonNetwork.JoinOrCreateRoom("Room name", new RoomOptions { MaxPlayers = 5, IsOpen = true, IsVisible = true }, TypedLobby.Default);
        }
    }

    public override void OnJoinedRoom()
    {
        // This method is called when the player successfully joins a room.
        Debug.Log("Joined Room");

        // Deactivates the login panel UI element as the player has now joined a room.
        LoginPanel.SetActive(false);

        // Sets the flag indicating the player is in-game.
        isInGame = true;

        // If the player is in the game, activate the in-game buttons UI.
        if (isInGame) { inGameButtons.SetActive(true); }

        // Starts a coroutine to handle player spawning after a short delay.
        StartCoroutine(waitTimeForPlayerSpawn(0.1f));
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.Log("Couldnt enter the Room" + message + " - " + returnCode);
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log("Couldnt enter the Random Room" + message + " - " + returnCode);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.Log("Couldnt create the Room" + message + " - " + returnCode);

    }

    public override void OnLeftLobby()
    {
        Debug.Log("Left Lobby");

    }

    public override void OnLeftRoom()
    {
        Debug.Log("Left Room");
    }
    public void DisconnectPlayer()
    {
        //LoginPanel.SetActive(false);
    }

    public void GetServerStatistics()
    {
        PhotonNetwork.NetworkStatisticsEnabled = true;
        Debug.Log(PhotonNetwork.NetworkStatisticsToString());
    }
    
    public void ResetServerStatistics()
    {
        PhotonNetwork.NetworkStatisticsReset();
    }

    public void GetPing()
    {
        //someMethod(PhotonNetwork.GetPing().ToString());
    }

    IEnumerator waitTimeForPlayerSpawn(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        //PhotonNetwork.Instantiate("Player", Vector3.zero, Quaternion.identity).GetComponent<Player>().UserID_Player = UserID_E;
        var player = PhotonNetwork.Instantiate("Player", Vector3.zero, Quaternion.identity).GetComponent<Player>();
        pw = player.GetComponent<PhotonView>(); 
        player.UserID_Player = UserID_E;
    }
}
