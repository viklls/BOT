using RestSharp;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace coursework
{
    public class AniBot
    {
        private readonly string _botToken = "7248221442:AAFVh4az5KyCt6y8uisGwTeDBFkO9c9VrVA";  // Замените на ваш токен
        private readonly TelegramBotClient botClient;
        private readonly ReceiverOptions receiverOptions;
        private readonly RestClient Client;
        private readonly CancellationTokenSource cts;
        private readonly Dictionary<long, string> lastCommands;
        private readonly List<string> jokes;
        private readonly List<string> quotes;
        private readonly Random random;

        public AniBot()
        {
            botClient = new TelegramBotClient(_botToken);
            cts = new CancellationTokenSource();
            receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };
            Client = new RestClient("https://localhost:7270");
            lastCommands = new Dictionary<long, string>();
            random = new Random();

            // Инициализация списков шуток и цитат
            jokes = new List<string>
            {
                "What would you call a French fan of anime?\nA Ouib!",
                "Why can't Naruto watch scary movies?\nHe's too sensei-tive!",
                "What do u call an old avatar?\nBoomer-Aang!",
                "How does Piccolo relax?\nhe goes to Planet Hammock.",
                "The creators of Haikyu sued a rival show for plagurism!\nIt was a court case!"
            };

            quotes = new List<string>
            {
                "<Ikigai> - the reason for being",
                "If u can dream it\nthen u can do it",
                "The more u trust, the greater the betrayal.\nThe more u love, the graeter the harm.",
                "Somethings are better left unsaid...",
                "<Ukiyo> - living in the moment"
            };
        }

        public async Task Start()
        {
            try
            {
                botClient.StartReceiving(
                    HandlerUpdateAsync,
                    HandlerErrorAsync,
                    receiverOptions,
                    cancellationToken: cts.Token);

                var botMe = await botClient.GetMeAsync();
                Console.WriteLine($"Bot {botMe.Username} started working");

                // Keep the console application running
                while (!cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in Start: {ex.Message}");
            }
        }

        private async Task HandlerErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };
            Console.WriteLine(errorMessage);

            // Поскольку chatId не доступен в контексте ошибок, нельзя отправить сообщение в конкретный чат.
            // Можно логировать ошибку или предпринять другие действия.
        }

        private async Task HandlerUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.Message && update.Message?.Text != null)
            {
                await HandlerMessageAsync(botClient, update.Message);
            }
        }

        private async Task HandlerMessageAsync(ITelegramBotClient botClient, Message message)
        {
            var chatId = message.Chat.Id;
            var messageText = message.Text;

            if (messageText.Equals("/start", StringComparison.OrdinalIgnoreCase))
            {
                var startKeyboard = new ReplyKeyboardMarkup(new[]
                {
                    new KeyboardButton[] { "Start" }
                })
                {
                    ResizeKeyboard = true
                };

                await botClient.SendTextMessageAsync(chatId, "Press Start to begin", replyMarkup: startKeyboard);
            }
            else if (messageText.Equals("Start", StringComparison.OrdinalIgnoreCase))
            {
                await botClient.SendTextMessageAsync(chatId, "Welcome! I’ll help you brighten your day by watching some interesting anime or reading a novel. But what’s more interesting, you won’t know what you’re choosing) \n  Use the keyboard to see available options", replyMarkup: GetReplyKeyboard());
            }
            else if (messageText.Equals("/keyboard", StringComparison.OrdinalIgnoreCase))
            {
                await botClient.SendTextMessageAsync(chatId, "Choose the menu option:", replyMarkup: GetReplyKeyboard());
            }
            else if (messageText.Equals("Anime article", StringComparison.OrdinalIgnoreCase))
            {
                await botClient.SendTextMessageAsync(chatId, "Enter an anime ID to find out the name");
                lastCommands[chatId] = "Anime article";
            }
            else if (messageText.Equals("Anime description", StringComparison.OrdinalIgnoreCase))
            {
                await botClient.SendTextMessageAsync(chatId, "Enter an anime ID to know more");
                lastCommands[chatId] = "Anime description";
            }
            else if (messageText.Equals("Novel article", StringComparison.OrdinalIgnoreCase))
            {
                await botClient.SendTextMessageAsync(chatId, "Enter a novel ID to find out the name");
                lastCommands[chatId] = "Novel article";
            }
            else if (messageText.Equals("Novel description", StringComparison.OrdinalIgnoreCase))
            {
                await botClient.SendTextMessageAsync(chatId, "Enter a novel ID to know more");
                lastCommands[chatId] = "Novel description";
            }
            else if (int.TryParse(messageText, out int id))
            {
                if (lastCommands.TryGetValue(chatId, out string lastCommand))
                {
                    if (lastCommand == "Anime article")
                    {
                        var title = await GetAnimeTitleById(id);
                        await botClient.SendTextMessageAsync(chatId, title);
                    }
                    else if (lastCommand == "Anime description")
                    {
                        var description = await GetAnimeDescriptionById(id);
                        await botClient.SendTextMessageAsync(chatId, description);
                    }
                    else if (lastCommand == "Novel article")
                    {
                        var title = await GetNovelNameById(id);
                        await botClient.SendTextMessageAsync(chatId, title);
                    }
                    else if (lastCommand == "Novel description")
                    {
                        var description = await GetNovelDescriptionById(id);
                        await botClient.SendTextMessageAsync(chatId, description);
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(chatId, "Invalid command.");
                    }

                    // Спрашиваем про шутку или цитату после предоставления информации
                    await botClient.SendTextMessageAsync(chatId, "What would you like to hear now?", replyMarkup: GetJokeOrQuoteKeyboard());
                }
                else
                {
                    await botClient.SendTextMessageAsync(chatId, "Invalid command.");
                }
            }
            else if (messageText.Equals("Joke", StringComparison.OrdinalIgnoreCase))
            {
                await botClient.SendTextMessageAsync(chatId, GetRandomJoke());
                await botClient.SendTextMessageAsync(chatId, "Well, I hope you enjoyed my company and I’ve been helpful to you. Feel free to contact me anytime!");

                // Отображаем начальную клавиатуру после шутки или цитаты
                await botClient.SendTextMessageAsync(chatId, "You can choose another option.", replyMarkup: GetReplyKeyboard());
            }
            else if (messageText.Equals("Quote", StringComparison.OrdinalIgnoreCase))
            {
                await botClient.SendTextMessageAsync(chatId, GetRandomQuote());
                await botClient.SendTextMessageAsync(chatId, "Well, I hope you enjoyed my company and I’ve been helpful to you. Feel free to contact me anytime!");

                // Отображаем начальную клавиатуру после шутки или цитаты
                await botClient.SendTextMessageAsync(chatId, "You can use the keyboard, Im ready to help you every single moment!", replyMarkup: GetReplyKeyboard());
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId, "I don't understand your command. Please use the keyboard to see available options.", replyMarkup: GetReplyKeyboard());
            }
        }

        private async Task<string> GetAnimeTitleById(int id)
        {
            var request = new RestRequest($"/api/anime/title/{id}", Method.Get);
            var response = await Client.ExecuteAsync(request);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = response.Content.Replace("\"", "");
                Console.WriteLine("Response content: " + content);
                return DecodeUnicodeCharacters(content);
            }
            else
            {
                Console.WriteLine($"Error: {response.StatusCode} - {response.ErrorMessage}");
                return "Error retrieving anime title.";
            }
        }

        private async Task<string> GetAnimeDescriptionById(int id)
        {
            var request = new RestRequest($"/api/anime/description/{id}", Method.Get);
            var response = await Client.ExecuteAsync(request);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = response.Content.Replace("\"", "");
                Console.WriteLine("Response content: " + content);
                return DecodeUnicodeCharacters(content);
            }
            else
            {
                Console.WriteLine($"Error: {response.StatusCode} - {response.ErrorMessage}");
                return "Error retrieving anime description.";
            }
        }

        private async Task<string> GetNovelNameById(int id)
        {
            var request = new RestRequest($"/api/novel/name/{id}", Method.Get);
            var response = await Client.ExecuteAsync(request);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = response.Content.Replace("\"", "");
                Console.WriteLine("Response content: " + content);
                return DecodeUnicodeCharacters(content);
            }
            else
            {
                Console.WriteLine($"Error: {response.StatusCode} - {response.ErrorMessage}");
                return "Error retrieving novel name.";
            }
        }

        private async Task<string> GetNovelDescriptionById(int id)
        {
            var request = new RestRequest($"/api/novel/description/{id}", Method.Get);
            var response = await Client.ExecuteAsync(request);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = response.Content.Replace("\"", "");
                Console.WriteLine("Response content: " + content);
                return DecodeUnicodeCharacters(content);
            }
            else
            {
                Console.WriteLine($"Error: {response.StatusCode} - {response.ErrorMessage}");
                return "Error retrieving novel description.";
            }
        }

        private string DecodeUnicodeCharacters(string input)
        {
            return Regex.Unescape(input);
        }

        private IReplyMarkup GetReplyKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "Anime article", "Anime description" },
                new KeyboardButton[] { "Novel article", "Novel description" }
            })
            {
                ResizeKeyboard = true
            };
        }

        private IReplyMarkup GetJokeOrQuoteKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "Joke", "Quote" }
            })
            {
                ResizeKeyboard = true
            };
        }

        private string GetRandomJoke()
        {
            int index = random.Next(jokes.Count);
            return jokes[index];
        }

        private string GetRandomQuote()
        {
            int index = random.Next(quotes.Count);
            return quotes[index];
        }

        public async Task Stop()
        {
            cts.Cancel();
            
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var bot = new AniBot();
            await bot.Start();
        }
    }
}
