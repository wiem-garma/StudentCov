using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CovStudent.Tests;
[Trait("Categorie", "Integration")]

public class IntegrationTests : IClassFixture<CovStudentFactory>
{
    private readonly CovStudentFactory _factory;

    public IntegrationTests(CovStudentFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Client(string? userId = null, string? role = null)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        if (userId != null)
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId);
        if (role != null)
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);

        return client;
    }

    // ---------- Recherche (fonctionnel + volume) ----------

    [Fact]
    public async Task Recherche_affiche_uniquement_les_trajets_a_venir_avec_pagination()
    {
        var reponse = await Client().GetAsync("/Trip");
        var html = await reponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Contains("Résultats (25)", html);   // 20 + 5, sans le trajet passé
        Assert.Contains("Page 1 / 2", html);       // 20 par page
    }

    [Fact]
    public async Task Recherche_page_2_affiche_la_suite()
    {
        var reponse = await Client().GetAsync("/Trip?page=2");
        var html = await reponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Contains("Page 2 / 2", html);
    }

    [Fact]
    public async Task Recherche_filtre_par_ville_de_depart()
    {
        var reponse = await Client().GetAsync("/Trip?departure=Sfax");
        var html = await reponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Contains($"Résultats ({CovStudentFactory.NbTrajetsSfax})", html);
    }

    [Fact]
    public async Task Detail_d_un_trajet_inexistant_renvoie_404()
    {
        var reponse = await Client().GetAsync("/Trip/Details/999999");

        Assert.Equal(HttpStatusCode.NotFound, reponse.StatusCode);
    }

    // ---------- Sécurité : contrôle d'accès ----------

    [Theory]
    [InlineData("/Trip/Create")]
    [InlineData("/Audit")]
    public async Task Page_protegee_refusee_a_un_visiteur_anonyme(string url)
    {
        var reponse = await Client().GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);
    }

    [Fact]
    public async Task Journal_d_audit_interdit_a_un_utilisateur_non_admin()
    {
        var reponse = await Client("passager").GetAsync("/Audit");

        Assert.Equal(HttpStatusCode.Forbidden, reponse.StatusCode);
    }

    [Fact]
    public async Task Journal_d_audit_accessible_a_l_admin()
    {
        var reponse = await Client("admin", "Admin").GetAsync("/Audit");
        var html = await reponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Contains("Journal d'audit", html);
    }

    [Fact]
    public async Task Publication_de_trajet_accessible_a_un_utilisateur_connecte()
    {
        var reponse = await Client("conducteur").GetAsync("/Trip/Create");

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
    }
}