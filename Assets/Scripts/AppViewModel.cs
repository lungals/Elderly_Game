using System;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// ViewModel usado como DataSource do UI Toolkit (Unity 6+).
/// Cada propriedade [CreateProperty] pode ser ligada no UXML via data-source-path.
/// </summary>
public class AppViewModel : INotifyBindablePropertyChanged
{
    public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

    void Notificar(string propriedade) =>
        propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(propriedade));

    // Atribui o campo e notifica a UI. Propriedades dependentes são notificadas junto.
    bool Set<T>(ref T campo, T valor, string nome, params string[] dependentes)
    {
        if (Equals(campo, valor)) return false;
        campo = valor;
        Notificar(nome);
        foreach (var d in dependentes) Notificar(d);
        return true;
    }

    // ---------- Menu ----------
    [CreateProperty] public string TituloApp { get; } = "COMPARTILHANDO HISTÓRIAS";

    // ---------- Conta ----------
    string _login = "";
    string _email = "";
    string _nome = "NOME";
    bool _whatsAppVinculado;
    string _mensagemErro = "";

    [CreateProperty] public string Login { get => _login; set => Set(ref _login, value, nameof(Login)); }
    [CreateProperty] public string Email { get => _email; set => Set(ref _email, value, nameof(Email), nameof(Informacoes)); }
    [CreateProperty] public string Nome { get => _nome; set => Set(ref _nome, value, nameof(Nome)); }
    [CreateProperty] public string MensagemErro { get => _mensagemErro; set => Set(ref _mensagemErro, value, nameof(MensagemErro)); }

    [CreateProperty]
    public bool WhatsAppVinculado
    {
        get => _whatsAppVinculado;
        set => Set(ref _whatsAppVinculado, value, nameof(WhatsAppVinculado), nameof(TextoWhatsApp), nameof(Informacoes));
    }

    [CreateProperty] public string TextoWhatsApp => WhatsAppVinculado ? "WHATSAPP VINCULADO ✓" : "VINCULAR COM WHATSAPP";

    [CreateProperty]
    public string Informacoes =>
        $"{(string.IsNullOrEmpty(Email) ? "sem email" : Email)}\nWhatsApp: {(WhatsAppVinculado ? "vinculado" : "não vinculado")}";

    // ---------- Tela principal ----------
    Texture _imagemAtual;
    bool _curtidaAtual;
    bool _tocandoAudio;
    bool _temAudio;

    [CreateProperty] public Texture ImagemAtual { get => _imagemAtual; set => Set(ref _imagemAtual, value, nameof(ImagemAtual), nameof(TextoSemImagem)); }
    [CreateProperty] public string TextoSemImagem => _imagemAtual == null ? "IMAGEM" : "";

    [CreateProperty] public bool CurtidaAtual { get => _curtidaAtual; set => Set(ref _curtidaAtual, value, nameof(CurtidaAtual), nameof(TextoGostei)); }
    [CreateProperty] public string TextoGostei => CurtidaAtual ? "CURTIDO" : "GOSTEI";

    [CreateProperty] public bool TocandoAudio { get => _tocandoAudio; set => Set(ref _tocandoAudio, value, nameof(TocandoAudio), nameof(TextoAudio)); }
    [CreateProperty] public bool TemAudio { get => _temAudio; set => Set(ref _temAudio, value, nameof(TemAudio), nameof(TextoAudio)); }
    [CreateProperty] public string TextoAudio => !TemAudio ? "SEM ÁUDIO" : (TocandoAudio ? "■ PARAR ÁUDIO" : "▶ TOCAR ÁUDIO");

    // ---------- Câmera ----------
    Texture _cameraTexture;
    bool _gravando;

    [CreateProperty] public Texture CameraTexture { get => _cameraTexture; set => Set(ref _cameraTexture, value, nameof(CameraTexture), nameof(TextoCamera)); }
    [CreateProperty] public string TextoCamera => _cameraTexture == null ? "IMAGEM\nCÂMERA" : "";

    [CreateProperty] public bool Gravando { get => _gravando; set => Set(ref _gravando, value, nameof(Gravando), nameof(TextoGravar)); }
    [CreateProperty] public string TextoGravar => Gravando ? "PARAR GRAVAÇÃO" : "GRAVE ÁUDIO";
}
