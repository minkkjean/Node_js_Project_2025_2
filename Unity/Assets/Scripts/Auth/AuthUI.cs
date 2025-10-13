using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.UI;
using UnityEngine.UI;
using System.Net;
public class AuthUI : MonoBehaviour
{
    public InputField usernameInput;
    public InputField passwordInput;

    public Button registerButton;
    public Button loginButton;

    public Text statusText;

    private AutuManager autuManager;
    // Start is called before the first frame update
    void Start()
    {
        autuManager = GetComponent<AutuManager>();
        registerButton.onClick.AddListener(OnRegisterClick);
        loginButton.onClick.AddListener(OnLoginClick);
    }

    private void OnRegisterClick()
    {
        StartCoroutine(RegisterCoroutine());
    }

    private void OnLoginClick()
    {
        StartCoroutine(LoginCoroutine());
    }

    private IEnumerator LoginCoroutine()
    {
        statusText.text = "로그인 중 ....";
        yield return StartCoroutine(autuManager.Login(usernameInput.text, passwordInput.text));
        statusText.text = "로그인 성공";
    }

    private IEnumerator RegisterCoroutine()
    {
        statusText.text = "회원 가입 중 ....";
        yield return StartCoroutine(autuManager.Register(usernameInput.text, passwordInput.text));
        statusText.text = "회원가입 성공 , 로그인 해주세요";
    }
}
