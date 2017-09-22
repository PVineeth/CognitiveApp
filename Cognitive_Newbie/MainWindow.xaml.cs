using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Cognitive_Newbie
{

    using System.Threading;
    using System.Windows.Threading;
    using Microsoft.CognitiveServices.SpeechRecognition;
    using System.Net.Http;
    using Newtonsoft.Json;
    using System.Web;
    using Cognitive_Newbie.Model;
    using System.Net.Http.Headers;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private MicrophoneRecognitionClient micClient;
        public MainWindow()
        {
            InitializeComponent();
            this.micClient = SpeechRecognitionServiceFactory.CreateMicrophoneClient(
               SpeechRecognitionMode.ShortPhrase,
               "en-US",
               "c33fdf2cdcdc429bb3e3b424c502f6cc");
            this.micClient.OnMicrophoneStatus += MicClient_OnMicrophoneStatus;
            this.micClient.OnResponseReceived += MicClient_OnResponseReceived;
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            this.MySpeechSentiment.Visibility = Visibility.Visible;
            this.MySpeechSentimentConfidence.Visibility = Visibility.Visible;
            this.MySpeechSentiment.Text = string.Empty;
            this.MySpeechSentimentConfidence.Text = string.Empty;
            this.MySpeechResponse.Text = string.Empty;
            this.MySpeechResponseConfidence.Text = string.Empty;
            this.searchImage.Source = null;
            this.micClient.StartMicAndRecognition();
        }

        private void MicClient_OnMicrophoneStatus(object sender, MicrophoneEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                new Action(
                    () =>
                    {
                        if (e.Recording)
                        {
                            this.status.Text = "Listening";
                            this.RecordingBar.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            this.status.Text = "Not Listening";
                            this.RecordingBar.Visibility = Visibility.Collapsed;
                        }
                    }));
        }

        private async void MicClient_OnResponseReceived(object sender, SpeechResponseEventArgs e)
        {
            if (e.PhraseResponse.Results.Length > 0)
            {
                await Application.Current.Dispatcher.BeginInvoke(
                    DispatcherPriority.Normal, new Action(() =>
                    {
                        this.MySpeechResponse.Text = $"'{e.PhraseResponse.Results[0].DisplayText}',";
                        this.MySpeechResponseConfidence.Text = $"confidence: { e.PhraseResponse.Results[0].Confidence}";

                        //this.Speak(this.MySpeechResponse.Text);
                    }));
                this.SearchImage(e.PhraseResponse.Results[0].DisplayText);
            }
            
        }

        private async void SearchImage(string SearchTerm)
        {
            var client = new HttpClient();
            var queryString = HttpUtility.ParseQueryString(string.Empty);

            // Request headers
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "a47c81eec1ac46eabc0bf5472778f3ef");

            // Request parameters
            queryString["q"] = SearchTerm;
            queryString["count"] = "1";
            queryString["offset"] = "0";
            queryString["mkt"] = "en-us";
            queryString["safeSearch"] = "Moderate";
            var uri = "https://api.cognitive.microsoft.com/bing/v5.0/images/search?" + queryString;

            var response = await client.GetAsync(uri);
            var json = await response.Content.ReadAsStringAsync();
            // MessageBox.Show(json.ToString());
            BingImageSearchResponse bingImageSearchResponse = JsonConvert.DeserializeObject<BingImageSearchResponse>(json);
            var uriSource = new Uri(bingImageSearchResponse.value[0].contentUrl, UriKind.Absolute);

            await Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Normal, new Action(() =>
                {
                    this.searchImage.Source = new BitmapImage(uriSource);

                }));

            await GetEmotion(uriSource.ToString());
        }

        private async Task GetEmotion(string imageUri)
        {
            var client = new HttpClient();
            var queryString = HttpUtility.ParseQueryString(string.Empty);

            // Request headers
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "f70ca1d63dd348e5a4d602a9c3d212f4");

            // Request parameters
            var uri = "https://westus.api.cognitive.microsoft.com/emotion/v1.0/recognize?" + queryString;
            EmotionRequest request = new EmotionRequest();
            request.url = imageUri;
            byte[] byteData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request));

            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                var response = await client.PostAsync(uri, content);
                var json = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    List<EmotionResponse> emotionResponse =
                        JsonConvert.DeserializeObject<List<EmotionResponse>>(json);

                    if (emotionResponse != null && emotionResponse.Count > 0)
                    {
                        var scores = emotionResponse[0].scores;
                        Dictionary<string, double> dScores = new Dictionary<string, double>();
                        dScores.Add("anger", scores.anger);
                        dScores.Add("contempt", scores.contempt);
                        dScores.Add("disgust", scores.disgust);
                        dScores.Add("fear", scores.fear);
                        dScores.Add("happiness", scores.happiness);
                        dScores.Add("neutral", scores.neutral);
                        dScores.Add("sadness", scores.sadness);
                        dScores.Add("surprise", scores.surprise);
                        var highestScore = dScores.Values.OrderByDescending(score => score).First();
                        //probably a more elegant way to do this.
                        var highestEmotion = dScores.Keys.First(key => dScores[key] == highestScore);

                        await Application.Current.Dispatcher.BeginInvoke(
                            DispatcherPriority.Normal,
                            new Action(
                                () =>
                                {
                                    this.MySpeechSentiment.Text = $"Emotion: {highestEmotion},";
                                    this.MySpeechSentimentConfidence.Text =
                                        $"confidence: {Convert.ToInt16(highestScore * 100)}%";
                                }));

                    }
                    else
                    {
                        this.MySpeechSentiment.Text = $"I'm not able to get the emotion, sorry.";
                    }
                }
                else
                {
                    await Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Normal,
                        new Action(() => {
                            this.MySpeechSentiment.Text = "Could not get emotion from this image";
                        }));

                }
            }

        }
    }

}
    
