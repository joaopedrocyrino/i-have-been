using IHaveBeen.Domain.Experiences;
using IHaveBeen.Web.Components.Sharing;
using IHaveBeen.Web.Features.Experiences;
using IHaveBeen.Web.Features.Sharing;
using IHaveBeen.Web.Features.TravelLogs;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using IHaveBeen.Application.Media;

namespace IHaveBeen.Web.Components.Pages;

public partial class Dashboard
{
    private IJSObjectReference? module;
    private DotNetObjectReference<Dashboard>? reference;
    private List<LogView> logs = [];
    private List<ShareView> shares = [];
    private LogView? selected;
    private MediaUsage? mediaUsage;
    private LogInput draft = new();
    private Guid? editingId, experienceId;
    private ExperienceInput experienceDraft = new();
    private bool editingExperience;
    private bool loading = true, editing, sharing, busy;
    private string? notice, newShareUrl;
    private string mapCountry = "";
    private readonly ShareDraft shareDraft = new();
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        module = await JS.InvokeAsync<IJSObjectReference>("import", "/map.js");
        reference = DotNetObjectReference.Create(this);
        await module.InvokeVoidAsync("initialize", "map", reference);
        await Act(async () => { await Refresh(); loading = false; });
    }
    private async Task<T> Request<T>(string method, string path, object? data = null) => await module!.InvokeAsync<T>("request", method, path, data);
    private async Task Refresh()
    {
        logs = await Request<List<LogView>>("GET", "/api/logs");
        mediaUsage = await Request<MediaUsage>("GET", "/api/media/usage");
        if (selected is not null) selected = logs.FirstOrDefault(x => x.Id == selected.Id);
        await module!.InvokeVoidAsync("setLogs", logs);
    }
    private async Task Act(Func<Task> work)
    {
        busy = true; notice = null;
        try { await work(); } catch (JSException ex) { notice = ex.Message.Split('\n')[0]; } finally { busy = false; StateHasChanged(); }
    }
    private void NewLog() { draft = new() { Country = mapCountry }; editingId = null; editing = true; notice = null; }
    private void NewWish() { NewLog(); draft.IsWishlist = true; }
    [JSInvokable] public void CountrySelected(string country) { mapCountry = country; editing = false; selected = null; notice = null; StateHasChanged(); }
    [JSInvokable] public void ChoosePlace(double latitude, double longitude, string country) { draft = new() { Latitude = Math.Round(latitude, 6), Longitude = Math.Round(longitude, 6), Country = country }; editingId = null; editing = true; StateHasChanged(); }
    [JSInvokable] public async Task SelectLog(Guid id) { selected = logs.FirstOrDefault(x => x.Id == id); if (selected is not null) { mapCountry = selected.Country; await module!.InvokeVoidAsync("focusLog", selected); } StateHasChanged(); }
    private void EditLog()
    {
        if (selected is null) return;
        editingId = selected.Id; draft = new() { Title = selected.Title, Description = selected.Description, City = selected.City, Country = selected.Country, Latitude = selected.Latitude, Longitude = selected.Longitude, VisitedOn = selected.VisitedOn, EndedOn = selected.EndedOn, IncludeInShares = selected.IncludeInShares, IsWishlist = selected.IsWishlist, PlannedOn = selected.PlannedOn }; editing = true; notice = null;
    }
    private void MarkVisited() { EditLog(); draft.IsWishlist = false; draft.VisitedOn = DateOnly.FromDateTime(DateTime.UtcNow); }
    private void NewExperience() { experienceDraft = new(); experienceId = null; editingExperience = true; notice = null; }
    private void EditExperience(ExperienceView item)
    {
        experienceId = item.Id;
        experienceDraft = new() { Title = item.Title, Description = item.Description, Category = Enum.Parse<ExperienceCategory>(item.Category), Rating = item.Rating, Address = item.Address, VisitedOn = item.VisitedOn };
        editingExperience = true; notice = null;
    }
    private Task SaveExperience() => Act(async () =>
    {
        if (selected is null) return;
        var path = $"/api/logs/{selected.Id}/experiences" + (experienceId is null ? "" : $"/{experienceId}");
        await Request<ExperienceView>(experienceId is null ? "POST" : "PUT", path, experienceDraft);
        editingExperience = false; await Refresh();
    });
    private Task DeleteExperience(Guid id) => Act(async () =>
    {
        if (selected is null || !await module!.InvokeAsync<bool>("confirmAction", "Delete this city experience?")) return;
        await Request<object?>("DELETE", $"/api/logs/{selected.Id}/experiences/{id}"); await Refresh();
    });
    private Task SaveLog() => Act(async () => { var saved = await Request<LogView>(editingId is null ? "POST" : "PUT", editingId is null ? "/api/logs" : $"/api/logs/{editingId}", draft); selected = saved; editing = false; await Refresh(); });
    private Task DeleteLog() => Act(async () => { if (selected is null || !await module!.InvokeAsync<bool>("confirmAction", "Delete this memory and all its media?")) return; await Request<object?>("DELETE", $"/api/logs/{selected.Id}"); selected = null; await Refresh(); });
    private Task DeleteMedia(Guid id) => Act(async () => { if (!await module!.InvokeAsync<bool>("confirmAction", "Remove this original file?")) return; await Request<object?>("DELETE", $"/api/media/{id}"); await Refresh(); });
    private Task UploadMedia() => Act(async () =>
    {
        if (selected is null) return;
        // Large original transfers/scans can outlast Blazor's normal interop timeout.
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        try { await module!.InvokeVoidAsync("uploadFiles", timeout.Token, "media-files", selected.Id); }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested) { notice = "The upload took too long. Check your journal before trying again."; }
        finally { await Refresh(); }
    });
    private Task OpenShares() => Act(async () => { shares = await Request<List<ShareView>>("GET", "/api/shares"); sharing = true; newShareUrl = null; });
    private sealed record CreatedShare(Guid Id, string Path);
    private Task CreateShare() => Act(async () =>
    {
        DateTimeOffset? expiration = shareDraft.Hours == "never" ? null : shareDraft.Hours == "custom" ? DateTimeOffset.Parse(await module!.InvokeAsync<string>("toUtc", shareDraft.CustomExpiration)) : DateTimeOffset.UtcNow.AddHours(int.Parse(shareDraft.Hours));
        var created = await Request<CreatedShare>("POST", "/api/shares", new ShareInput(shareDraft.Label, expiration));
        newShareUrl = Navigation.BaseUri.TrimEnd('/') + created.Path; shares = await Request<List<ShareView>>("GET", "/api/shares");
    });
    private Task CopyShare() => module!.InvokeVoidAsync("copyText", newShareUrl).AsTask();
    private Task RevokeShare(Guid id) => Act(async () => { await Request<object?>("DELETE", $"/api/shares/{id}"); shares = await Request<List<ShareView>>("GET", "/api/shares"); newShareUrl = null; });
    public async ValueTask DisposeAsync() { reference?.Dispose(); if (module is not null) { try { await module.InvokeVoidAsync("dispose"); await module.DisposeAsync(); } catch (JSDisconnectedException) { } } }

}
