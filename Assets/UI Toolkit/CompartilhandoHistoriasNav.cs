using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Navegação entre as telas do CompartilhandoHistorias.uxml.
// Convenções de classe nos botões do UXML:
//   go--<nomeDaTela>  abre a tela com esse name
//   back              volta para a tela anterior
//   quit              fecha o jogo
//   like              alterna o coração (curtir/descurtir)
// Esc / botão "voltar" do Android também voltam uma tela.
[RequireComponent(typeof(UIDocument))]
public class CompartilhandoHistoriasNav : MonoBehaviour
{
    const string RootName = "chRoot";
    const string ScreenClass = "screen";
    const string HiddenClass = "hidden";
    const string GoPrefix = "go--";
    const string BackClass = "back";
    const string QuitClass = "quit";
    const string LikeClass = "like";
    const string LikedClass = "liked";

    [SerializeField] string firstScreen = "startScreen";

    readonly Stack<VisualElement> history = new Stack<VisualElement>();
    VisualElement root;
    VisualElement wiredRoot;
    VisualElement current;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        AttachToDocuments();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachToDocuments();

    static void AttachToDocuments()
    {
        foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
        {
            if (doc.GetComponent<CompartilhandoHistoriasNav>() != null) continue;
            if (doc.rootVisualElement?.Q(RootName) == null) continue;
            doc.gameObject.AddComponent<CompartilhandoHistoriasNav>();
        }
    }

    void OnEnable()
    {
        root = GetComponent<UIDocument>().rootVisualElement;
        if (root == null) return;

        if (wiredRoot != root)
        {
            root.Query<Button>().ForEach(Wire);
            root.RegisterCallback<NavigationCancelEvent>(_ => Back());
            wiredRoot = root;
        }
        history.Clear();
        Show(root.Q(firstScreen));
    }

    void Wire(Button button)
    {
        if (button.ClassListContains(BackClass))
        {
            button.clicked += Back;
            return;
        }
        if (button.ClassListContains(QuitClass))
        {
            button.clicked += Quit;
            return;
        }
        if (button.ClassListContains(LikeClass))
        {
            button.clicked += () => button.ToggleInClassList(LikedClass);
            return;
        }

        foreach (string className in button.GetClasses())
        {
            if (!className.StartsWith(GoPrefix)) continue;
            string target = className.Substring(GoPrefix.Length);
            button.clicked += () => Go(target);
            return;
        }
    }

    void Go(string screenName)
    {
        var screen = root.Q(screenName);
        if (screen == null)
        {
            Debug.LogWarning($"Tela '{screenName}' não existe no UXML.");
            return;
        }

        // Voltar para a tela inicial (ex.: "Sair da conta") recomeça o histórico.
        if (screenName == firstScreen) history.Clear();
        else if (current != null && current != screen) history.Push(current);

        Show(screen);
    }

    void Back()
    {
        if (history.Count > 0) Show(history.Pop());
    }

    void Show(VisualElement screen)
    {
        if (screen == null) return;
        current = screen;
        root.Query(className: ScreenClass).ForEach(s => s.EnableInClassList(HiddenClass, s != screen));
    }

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
