using Discord;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    // ===== CONFIG =====
    private static readonly ulong GUILD_ID = 1426233977508987082;
    private static readonly ulong SUPPORTER_ROLE_ID = 1469069621062664313; // Discord Supporter role ID
    private static readonly long ROBLOX_GAMEPASS_ID = 1699993169; // Roblox Game Pass ID
    private static readonly string PRIVACY_URL = "https://trojanhorseth.github.io/T.H/privacy.";
    private static readonly string TERMS_URL = "https://trojanhorseth.github.io/T.H/tos";
    private static readonly HttpClient http = new HttpClient();
    // ==================

    private DiscordSocketClient _client;
    private Random _random = new Random();

    static Task Main() => new Program().RunAsync();

    public async Task RunAsync()
    {
        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.All
        });

        _client.Ready += OnReady;
        _client.SlashCommandExecuted += OnSlashCommand;

        string token = Environment.GetEnvironmentVariable("BOT_TOKEN") ?? "";
        if (string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine("Error: BOT_TOKEN environment variable not set!");
            return;
        }

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        await Task.Delay(-1);
    }

    private async Task OnReady()
    {
        Console.WriteLine($"Connected as {_client.CurrentUser}");

        var guild = _client.GetGuild(GUILD_ID);
        if (guild == null) return;

        await RegisterCommands(guild);
    }

    private async Task RegisterCommands(SocketGuild guild)
    {
        // Core commands
        await guild.CreateApplicationCommandAsync(new SlashCommandBuilder().WithName("status").WithDescription("Bot status").Build());
        await guild.CreateApplicationCommandAsync(new SlashCommandBuilder().WithName("privacy").WithDescription("Privacy Policy").Build());
        await guild.CreateApplicationCommandAsync(new SlashCommandBuilder().WithName("terms").WithDescription("Terms of Service").Build());
        await guild.CreateApplicationCommandAsync(new SlashCommandBuilder().WithName("rules").WithDescription("Admin-only rules command").Build());
        await guild.CreateApplicationCommandAsync(new SlashCommandBuilder().WithName("supporter-menu").WithDescription("Supporter menu info").Build());
        
        // Supporter commands
        await guild.CreateApplicationCommandAsync(new SlashCommandBuilder().WithName("gen-code").WithDescription("Generate a supporter code").Build());
        await guild.CreateApplicationCommandAsync(new SlashCommandBuilder()
            .WithName("get-supporter")
            .WithDescription("Get supporter role if you own the Roblox Game Pass")
            .AddOption("username", ApplicationCommandOptionType.String, "Your Roblox username", isRequired: true)
            .Build());
    }

    private async Task OnSlashCommand(SocketSlashCommand command)
    {
        await command.DeferAsync(ephemeral: true);
        var guild = (command.Channel as SocketGuildChannel)?.Guild;
        var user = command.User as SocketGuildUser;

        switch (command.Data.Name)
        {
            case "status":
                await command.FollowupAsync($"🟢 Bot is online! Hello {command.User.Mention}");
                break;

            case "privacy":
                var privacyEmbed = new EmbedBuilder()
                    .WithTitle("🔒 Privacy Policy")
                    .WithDescription($"Read the full privacy policy here:\n[Privacy Policy]({PRIVACY_URL})")
                    .WithColor(Color.DarkGrey)
                    .Build();
                await command.FollowupAsync(embed: privacyEmbed);
                break;

            case "terms":
                var termsEmbed = new EmbedBuilder()
                    .WithTitle("📃 Terms of Service")
                    .WithDescription($"Read the official Terms of Service here:\n[Terms of Service]({TERMS_URL})")
                    .WithColor(Color.DarkGrey)
                    .Build();
                await command.FollowupAsync(embed: termsEmbed);
                break;

            case "rules":
                if (!user.GuildPermissions.Administrator)
                {
                    await command.FollowupAsync("❌ You do not have permission to run this command.", ephemeral: true);
                    return;
                }
                var rulesEmbed = new EmbedBuilder()
                    .WithTitle("📜 Server Rules")
                    .WithColor(Color.DarkGrey)
                    .WithDescription("Please follow these rules to keep the server safe and fun!")
                    .AddField("1️⃣ Be Respectful", "Treat everyone with respect. No harassment or hate speech.")
                    .AddField("2️⃣ No Spamming", "Avoid spamming messages, images, or emojis.")
                    .AddField("3️⃣ Follow Discord TOS", "All members must comply with Discord's Terms of Service.")
                    .Build();
                await command.FollowupAsync(embed: rulesEmbed);
                break;

            case "supporter-menu":
                var menuEmbed = new EmbedBuilder()
                    .WithTitle("💎 Supporter Menu")
                    .WithColor(Color.DarkGrey)
                    .WithDescription("Welcome to the TrojanHorse Supporter Menu! Here’s what supporters get:")
                    .AddField("✨ Exclusive Features", "Access to premium commands and early updates.")
                    .AddField("🛡️ Enhanced Privacy", "Extra protection for your account and data.")
                    .AddField("🎫 Redeem Codes", "Use `/gen-code` to generate a unique supporter code.")
                    .Build();
                await command.FollowupAsync(embed: menuEmbed);
                break;

            case "gen-code":
                if (!user.Roles.Any(r => r.Id == SUPPORTER_ROLE_ID))
                {
                    await command.FollowupAsync("❌ You must be a supporter to generate codes!");
                    return;
                }
                string code = GenerateCode();
                await command.FollowupAsync($"🎫 Your supporter code: `{code}`");
                break;

            case "get-supporter":
                string robloxUsername = command.Data.Options.First().Value.ToString();
                bool ownsPass = await CheckRobloxGamePass(robloxUsername);

                if (!ownsPass)
                {
                    await command.FollowupAsync("❌ You do not own the Roblox Game Pass.");
                    return;
                }

                var role = guild.GetRole(SUPPORTER_ROLE_ID);
                if (!user.Roles.Contains(role))
                    await user.AddRoleAsync(role);

                await command.FollowupAsync($"✅ Success! {user.Mention} is now a Supporter.");
                break;
        }
    }

    private string GenerateCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Range(0, 10).Select(_ => chars[_random.Next(chars.Length)]).ToArray());
    }

    private async Task<bool> CheckRobloxGamePass(string username)
    {
        try
        {
            // Step 1: Get the user ID by username
            var userResponse = await http.GetStringAsync($"https://users.roblox.com/v1/usernames/users");
            var postData = new
            {
                usernames = new string[] { username },
                excludeBannedUsers = true
            };
            var content = new StringContent(JsonSerializer.Serialize(postData), System.Text.Encoding.UTF8, "application/json");
            var response = await http.PostAsync("https://users.roblox.com/v1/usernames/users", content);
            var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

            // Debugging the output of Roblox username API response
            Console.WriteLine($"Roblox API Response: {json}");

            if (!json.TryGetProperty("data", out var data) || data.GetArrayLength() == 0) return false;

            int userId = data[0].GetProperty("id").GetInt32();

            // Step 2: Check if the user owns the game pass by fetching inventory
            var inventoryResponse = await http.GetStringAsync($"https://inventory.roblox.com/v1/users/{userId}/assets?assetType=GamePass");
            var inventoryJson = JsonDocument.Parse(inventoryResponse).RootElement;

            // Debugging the inventory response
            Console.WriteLine($"Inventory Check Response: {inventoryJson}");

            // Check if the user owns the game pass by matching the asset ID
            var ownedGamePass = inventoryJson
                .GetProperty("data")
                .EnumerateArray()
                .Any(item => item.GetProperty("assetId").GetInt64() == ROBLOX_GAMEPASS_ID);

            return ownedGamePass;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during ownership check: {ex.Message}");
            return false;
        }
    }
}
