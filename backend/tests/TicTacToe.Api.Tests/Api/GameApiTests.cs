using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TicTacToe.Api.Tests.Api;

/// <summary>
/// Exercises the real HTTP pipeline (routing, JSON, validation, filters, status codes) in memory.
/// Assertions read the raw JSON so they check the wire format, not the C# types.
/// </summary>
public class GameApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateSession_Returns201WithInitialState()
    {
        var response = await _client.PostAsJsonAsync("/api/sessions", new { mode = "TwoPlayer" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var state = await ReadJson(response);
        var sessionId = state.GetProperty("sessionId").GetGuid();
        Assert.Equal($"/api/sessions/{sessionId}", response.Headers.Location?.AbsolutePath);
        Assert.NotEqual(Guid.Empty, state.GetProperty("gameId").GetGuid());
        Assert.Equal(0, state.GetProperty("revision").GetInt32());
        Assert.Equal("TwoPlayer", state.GetProperty("mode").GetString());
        Assert.Equal("InProgress", state.GetProperty("status").GetString());
        Assert.Equal("X", state.GetProperty("currentPlayer").GetString());
        Assert.Equal(JsonValueKind.Null, state.GetProperty("winner").ValueKind);
        Assert.Empty(state.GetProperty("winningCells").EnumerateArray());
        Assert.Equal(9, state.GetProperty("board").GetArrayLength());
        Assert.All(state.GetProperty("board").EnumerateArray(), cell => Assert.Equal(JsonValueKind.Null, cell.ValueKind));
        Assert.Empty(state.GetProperty("moves").EnumerateArray());
        Assert.False(state.GetProperty("canUndo").GetBoolean());
        AssertScore(state.GetProperty("scoreboard"), 0, 0, 0);
    }

    [Fact]
    public async Task Move_ReturnsUpdatedStateWithHistory()
    {
        var state = await CreateSession();

        var response = await PostMove(state, "X", row: 1, column: 2);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var next = await ReadJson(response);
        Assert.Equal("X", next.GetProperty("board")[5].GetString());
        Assert.Equal("O", next.GetProperty("currentPlayer").GetString());
        Assert.Equal(1, next.GetProperty("revision").GetInt32());
        Assert.True(next.GetProperty("canUndo").GetBoolean());
        var move = Assert.Single(next.GetProperty("moves").EnumerateArray());
        Assert.Equal(1, move.GetProperty("number").GetInt32());
        Assert.Equal("X", move.GetProperty("player").GetString());
        Assert.Equal(1, move.GetProperty("row").GetInt32());
        Assert.Equal(2, move.GetProperty("column").GetInt32());
    }

    [Fact]
    public async Task ComputerMode_MoveResponseIncludesComputerReply()
    {
        var state = await CreateSession("Computer");

        var next = await ReadJson(await PostMove(state, "X", 0, 0));

        var moves = next.GetProperty("moves").EnumerateArray().ToList();
        Assert.Equal(2, moves.Count);
        Assert.Equal("O", moves[1].GetProperty("player").GetString());
        Assert.Equal((1, 1), (moves[1].GetProperty("row").GetInt32(), moves[1].GetProperty("column").GetInt32()));
        Assert.Equal("X", next.GetProperty("currentPlayer").GetString());
        Assert.Equal(1, next.GetProperty("revision").GetInt32());
    }

    [Fact]
    public async Task Win_IsReportedAndScoredOnce()
    {
        var state = await PlayCells(await CreateSession(), Lines.XWinsTopRow);

        Assert.Equal("Won", state.GetProperty("status").GetString());
        Assert.Equal("X", state.GetProperty("winner").GetString());
        Assert.Equal(JsonValueKind.Null, state.GetProperty("currentPlayer").ValueKind);
        Assert.Equal([0, 1, 2], state.GetProperty("winningCells").EnumerateArray().Select(c => c.GetInt32()));
        Assert.False(state.GetProperty("canUndo").GetBoolean());
        AssertScore(state.GetProperty("scoreboard"), 1, 0, 0);
    }

    [Fact]
    public async Task Get_DoesNotChangeScoreboard()
    {
        var state = await PlayCells(await CreateSession(), Lines.XWinsTopRow);

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(GameUrl(state))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(SessionUrl(state))).StatusCode);
        }

        AssertScore(await GetJson($"{SessionUrl(state)}/scoreboard"), 1, 0, 0);
    }

    [Theory]
    [InlineData("""{"player":"X","row":3,"column":0,"expectedRevision":0}""", "row")]
    [InlineData("""{"player":"X","row":0,"column":-1,"expectedRevision":0}""", "column")]
    [InlineData("""{"player":"X","row":0,"column":0,"expectedRevision":-1}""", "expectedRevision")]
    [InlineData("""{"player":"Z","row":0,"column":0,"expectedRevision":0}""", "$.player")]
    [InlineData("""{"player":"x","row":0,"column":0,"expectedRevision":0}""", "$.player")]
    [InlineData("""{"player":0,"row":0,"column":0,"expectedRevision":0}""", "$.player")]
    [InlineData("""{"player":"X","row":"0","column":0,"expectedRevision":0}""", "$.row")]
    [InlineData("""{"player":"X","column":0,"expectedRevision":0}""", "$")]
    [InlineData("""{"player":"X","row":0,"column":0}""", "$")]
    [InlineData("""{"player":"X","row":0,"column":0,"expectedRevision":0,"board":["X"]}""", "$.board")]
    [InlineData("""{"player":"X","row":0,"column":0,"expectedRevision":0,"winner":"X"}""", "$.winner")]
    [InlineData("""{"player":"X","row":0,""", "$")]
    public async Task InvalidMoveRequest_Returns400AndChangesNothing(string body, string errorKey)
    {
        var state = await CreateSession();

        var response = await _client.PostAsync($"{GameUrl(state)}/moves", new StringContent(body, Encoding.UTF8, "application/json"));

        var problem = await AssertProblem(response, HttpStatusCode.BadRequest, "ValidationFailed");
        Assert.True(problem.GetProperty("errors").TryGetProperty(errorKey, out _), $"Expected an error for '{errorKey}'.");
        var after = await GetJson(GameUrl(state));
        Assert.Equal(0, after.GetProperty("revision").GetInt32());
        Assert.Empty(after.GetProperty("moves").EnumerateArray());
    }

    [Theory]
    [InlineData("""{"mode":"Chess"}""")]
    [InlineData("""{"mode":"computer"}""")]
    [InlineData("""{"mode":"TwoPlayer, Computer"}""")]
    [InlineData("""{"mode":1}""")]
    [InlineData("""{"mode":null}""")]
    [InlineData("""{}""")]
    public async Task InvalidMode_Returns400(string body)
    {
        var response = await _client.PostAsync("/api/sessions", new StringContent(body, Encoding.UTF8, "application/json"));

        await AssertProblem(response, HttpStatusCode.BadRequest, "ValidationFailed");
    }

    [Fact]
    public async Task StaleRevision_Returns409AndChangesNothing()
    {
        var state = await CreateSession();
        Assert.Equal(HttpStatusCode.OK, (await PostMove(state, "X", 0, 0)).StatusCode);

        // The same request again (a double click or a retry) still says revision 0.
        var duplicate = await PostMove(state, "X", 0, 0);
        var otherCell = await PostMove(state, "O", 1, 1);

        await AssertProblem(duplicate, HttpStatusCode.Conflict, "StaleRevision");
        await AssertProblem(otherCell, HttpStatusCode.Conflict, "StaleRevision");
        var after = await GetJson(GameUrl(state));
        Assert.Equal(1, after.GetProperty("revision").GetInt32());
        Assert.Single(after.GetProperty("moves").EnumerateArray());
    }

    [Fact]
    public async Task OccupiedCell_Returns409()
    {
        var state = await ReadJson(await PostMove(await CreateSession(), "X", 0, 0));

        await AssertProblem(await PostMove(state, "O", 0, 0), HttpStatusCode.Conflict, "CellOccupied");
    }

    [Fact]
    public async Task WrongPlayer_Returns409()
    {
        var state = await ReadJson(await PostMove(await CreateSession(), "X", 0, 0));

        await AssertProblem(await PostMove(state, "X", 1, 1), HttpStatusCode.Conflict, "WrongPlayer");
    }

    [Fact]
    public async Task ComputerMode_PlayingAsO_Returns409()
    {
        var state = await ReadJson(await PostMove(await CreateSession("Computer"), "X", 0, 0));

        await AssertProblem(await PostMove(state, "O", 2, 2), HttpStatusCode.Conflict, "WrongPlayer");
    }

    [Fact]
    public async Task MoveAndUndoAfterCompletion_Return409()
    {
        var state = await PlayCells(await CreateSession(), Lines.XWinsTopRow);

        await AssertProblem(await PostMove(state, "O", 2, 2), HttpStatusCode.Conflict, "GameCompleted");
        await AssertProblem(await PostUndo(state), HttpStatusCode.Conflict, "GameCompleted");
        AssertScore(await GetJson($"{SessionUrl(state)}/scoreboard"), 1, 0, 0);
    }

    [Fact]
    public async Task Undo_TwoPlayer_RemovesLastMove()
    {
        var state = await PlayCells(await CreateSession(), [0, 4]);

        var response = await PostUndo(state);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = await ReadJson(response);
        Assert.Single(after.GetProperty("moves").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, after.GetProperty("board")[4].ValueKind);
        Assert.Equal("O", after.GetProperty("currentPlayer").GetString());
        Assert.Equal(3, after.GetProperty("revision").GetInt32());
    }

    [Fact]
    public async Task Undo_Computer_RemovesMovePair()
    {
        var state = await PlayCells(await CreateSession("Computer"), [0]);

        var after = await ReadJson(await PostUndo(state));

        Assert.Empty(after.GetProperty("moves").EnumerateArray());
        Assert.Equal("X", after.GetProperty("currentPlayer").GetString());
        await AssertProblem(await PostUndo(after), HttpStatusCode.Conflict, "NothingToUndo");
    }

    [Fact]
    public async Task ResetGame_ReturnsFreshGameAndKeepsScore()
    {
        var finished = await PlayCells(await CreateSession(), Lines.XWinsTopRow);

        var response = await _client.PostAsync($"{GameUrl(finished)}/reset", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fresh = await ReadJson(response);
        Assert.NotEqual(finished.GetProperty("gameId").GetGuid(), fresh.GetProperty("gameId").GetGuid());
        Assert.Equal(finished.GetProperty("sessionId").GetGuid(), fresh.GetProperty("sessionId").GetGuid());
        Assert.Equal("TwoPlayer", fresh.GetProperty("mode").GetString());
        Assert.Equal("InProgress", fresh.GetProperty("status").GetString());
        Assert.Equal("X", fresh.GetProperty("currentPlayer").GetString());
        Assert.Equal(0, fresh.GetProperty("revision").GetInt32());
        Assert.Empty(fresh.GetProperty("moves").EnumerateArray());
        Assert.Empty(fresh.GetProperty("winningCells").EnumerateArray());
        AssertScore(fresh.GetProperty("scoreboard"), 1, 0, 0);
    }

    [Fact]
    public async Task RetiredGameId_Returns404()
    {
        var oldGame = await ReadJson(await PostMove(await CreateSession(), "X", 0, 0));
        var fresh = await ReadJson(await _client.PostAsync($"{GameUrl(oldGame)}/reset", null));

        await AssertProblem(await PostMove(oldGame, "O", 1, 1), HttpStatusCode.NotFound, "GameNotFound");
        await AssertProblem(await PostUndo(oldGame), HttpStatusCode.NotFound, "GameNotFound");
        await AssertProblem(await _client.GetAsync(GameUrl(oldGame)), HttpStatusCode.NotFound, "GameNotFound");

        var current = await GetJson(SessionUrl(fresh));
        Assert.Equal(fresh.GetProperty("gameId").GetGuid(), current.GetProperty("gameId").GetGuid());
        Assert.Empty(current.GetProperty("moves").EnumerateArray());
    }

    [Fact]
    public async Task StartNewGame_SwitchesModeAndKeepsScore()
    {
        var finished = await PlayCells(await CreateSession(), Lines.XWinsTopRow);

        var response = await _client.PostAsJsonAsync($"{SessionUrl(finished)}/games", new { mode = "Computer" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var fresh = await ReadJson(response);
        Assert.Equal(GameUrl(fresh), response.Headers.Location?.AbsolutePath);
        Assert.Equal("Computer", fresh.GetProperty("mode").GetString());
        Assert.Empty(fresh.GetProperty("moves").EnumerateArray());
        AssertScore(fresh.GetProperty("scoreboard"), 1, 0, 0);
    }

    [Fact]
    public async Task ResetScoreboard_ClearsCountsAndKeepsGame()
    {
        var finished = await PlayCells(await CreateSession(), Lines.XWinsTopRow);

        var response = await _client.PostAsync($"{SessionUrl(finished)}/scoreboard/reset", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertScore(await ReadJson(response), 0, 0, 0);
        var after = await GetJson(SessionUrl(finished));
        Assert.Equal("Won", after.GetProperty("status").GetString());
        Assert.Equal(finished.GetProperty("revision").GetInt32(), after.GetProperty("revision").GetInt32());
        Assert.Equal(5, after.GetProperty("moves").GetArrayLength());
        AssertScore(after.GetProperty("scoreboard"), 0, 0, 0);
    }

    [Fact]
    public async Task UnknownSession_Returns404()
    {
        var url = $"/api/sessions/{Guid.NewGuid()}";

        await AssertProblem(await _client.GetAsync(url), HttpStatusCode.NotFound, "SessionNotFound");
        await AssertProblem(await _client.GetAsync($"{url}/scoreboard"), HttpStatusCode.NotFound, "SessionNotFound");
        await AssertProblem(await _client.PostAsync($"{url}/scoreboard/reset", null), HttpStatusCode.NotFound, "SessionNotFound");
        await AssertProblem(
            await _client.PostAsJsonAsync($"{url}/games/{Guid.NewGuid()}/moves", new { player = "X", row = 0, column = 0, expectedRevision = 0 }),
            HttpStatusCode.NotFound,
            "SessionNotFound");
    }

    [Fact]
    public async Task Sessions_HaveSeparateScoreboards()
    {
        var a = await PlayCells(await CreateSession(), Lines.XWinsTopRow);
        var b = await CreateSession();

        AssertScore(await GetJson($"{SessionUrl(a)}/scoreboard"), 1, 0, 0);
        AssertScore(await GetJson($"{SessionUrl(b)}/scoreboard"), 0, 0, 0);
    }

    private static string SessionUrl(JsonElement state) => $"/api/sessions/{state.GetProperty("sessionId").GetGuid()}";

    private static string GameUrl(JsonElement state) => $"{SessionUrl(state)}/games/{state.GetProperty("gameId").GetGuid()}";

    private async Task<JsonElement> CreateSession(string mode = "TwoPlayer")
    {
        var response = await _client.PostAsJsonAsync("/api/sessions", new { mode });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadJson(response);
    }

    private Task<HttpResponseMessage> PostMove(JsonElement state, string player, int row, int column) =>
        _client.PostAsJsonAsync(
            $"{GameUrl(state)}/moves",
            new { player, row, column, expectedRevision = state.GetProperty("revision").GetInt32() });

    private Task<HttpResponseMessage> PostUndo(JsonElement state) =>
        _client.PostAsJsonAsync($"{GameUrl(state)}/undo", new { expectedRevision = state.GetProperty("revision").GetInt32() });

    /// <summary>Plays cell indexes as the player to move, each request using the revision just returned.</summary>
    private async Task<JsonElement> PlayCells(JsonElement state, int[] cells)
    {
        foreach (var cell in cells)
        {
            var response = await PostMove(state, state.GetProperty("currentPlayer").GetString()!, cell / 3, cell % 3);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            state = await ReadJson(response);
        }

        return state;
    }

    private async Task<JsonElement> GetJson(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJson(response);
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static async Task<JsonElement> AssertProblem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(" at TicTacToe.", text); // no stack trace
        var problem = JsonDocument.Parse(text).RootElement;
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.Equal(code, problem.GetProperty("code").GetString());
        return problem;
    }

    private static void AssertScore(JsonElement scoreboard, int xWins, int oWins, int draws) =>
        Assert.Equal(
            (xWins, oWins, draws),
            (scoreboard.GetProperty("xWins").GetInt32(), scoreboard.GetProperty("oWins").GetInt32(), scoreboard.GetProperty("draws").GetInt32()));
}
