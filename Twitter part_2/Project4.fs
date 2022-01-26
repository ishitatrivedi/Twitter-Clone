
open Suave
open Suave.Http
open Suave.Operators
open Suave.Filters
open Suave.Successful
open Suave.Files
open Suave.RequestErrors
open Suave.Logging
open Suave.Utils

open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket
open Newtonsoft.Json

open System
open System.Net
open System.Text.Json
open System.Collections.Generic

// Request class for serialization and deserialization of JSON messages

type Obj_for_Request =
    {
        Job: string
        User_id: string
        Pass_word: string
        Target_user_id: string
        Tweet: string
        Hashtag: string
        Mention: string
    }

// Response class for serialization and deserialization of JSON messages
type Obj_for_Response (resp_job: string, resp_status:string, resp_msg: string, resp_data: List<string>) = 
  member this.ResponseTask = resp_job
  member this.ResponseStatus = resp_status
  member this.ResponseMessage = resp_msg
  member this.ResponseData = resp_data

type User =
    {
        _First_Name : string
        _Last_Name : string
    }

// default response message values
let _response_job = ""
let _response_status = ""
let _response_msg = ""
let _response_data = new List<string>()

// Database for the twitter clone
let dict_users_table = new Dictionary<string, string>()
let dict_logged_in = new Dictionary<string, bool>()
let dict_followers_table = new Dictionary<string, List<string>>()
let dict_following_table = new Dictionary<string, List<string>>()
let dict_newsfeed_table = new Dictionary<string, List<string>>()
let dict_hashtags_table = new Dictionary<string, List<string>>()
let dict_mentions_table = new Dictionary<string, List<string>>()
let dict_webSocket_map = new Dictionary<string, WebSocket>()

let user_registered_(user_id) = 
  dict_users_table.ContainsKey(user_id)

let user_logged_in_(user_id) = 
  dict_logged_in.[user_id]

let validate_password(user_id, given_password_) = 
  let actual_password_ = dict_users_table.[user_id]

  actual_password_ = given_password_

// Helper function to process tweets and segregate into querys
let processing_tweets(tweeted_tweet:string) = 
  let mutable _mention_ = ""
  let mutable _hashtag_ = ""
  let list_hashtag = new List<string>()
  let list_mention = new List<string>()

  let mutable ii = 0;

  while ii < tweeted_tweet.Length do
    if tweeted_tweet.[ii] = '#' then
        while ii < tweeted_tweet.Length && tweeted_tweet.[ii] <> ' ' do
            _hashtag_ <- _hashtag_ + (tweeted_tweet.[ii] |> string)
            ii <- ii + 1

        if _hashtag_.Length > 1 then
          list_hashtag.Add(_hashtag_)

        _hashtag_ <- ""

    elif tweeted_tweet.[ii] = '@' then
        while ii < tweeted_tweet.Length && tweeted_tweet.[ii] <> ' ' do
            _mention_ <- _mention_ + (tweeted_tweet.[ii] |> string)
            ii <- ii + 1

        if _mention_.Length > 1 then
          list_mention.Add(_mention_)
          
        _mention_ <- ""

    ii <- ii + 1
  
  for _hashtag_ in list_hashtag do
    if dict_hashtags_table.ContainsKey(_hashtag_) then
        dict_hashtags_table.[_hashtag_].Add(tweeted_tweet)
    else
        dict_hashtags_table.Add(_hashtag_, new List<(string)>())
        dict_hashtags_table.[_hashtag_].Add(tweeted_tweet)

  for _mention_ in list_mention do
    if dict_mentions_table.ContainsKey(_mention_) then
        dict_mentions_table.[_mention_].Add(tweeted_tweet)
    else
        dict_mentions_table.Add(_mention_, new List<(string)>())
        dict_mentions_table.[_mention_].Add(tweeted_tweet)    

// Helper function to fetch the feed for every user

let feed_fetcher (user_id) =
  if (not (user_logged_in_(user_id))) then
    let my_feed_obj = Obj_for_Response("send_a_tweet", "0", ". User is not logged in. To see the timeline log in first", _response_data)
    my_feed_obj
  else
    let newsFeed = dict_newsfeed_table.[user_id]

    let my_feed_obj = Obj_for_Response("send_a_tweet", "1", "User Timeline has been refreshed", newsFeed)
    my_feed_obj


//Function to seralize the json object into bytes

let serialization_byte(query_response: Obj_for_Response) = 
  // seralize object to JSON string first
  let json_response_str = JsonSerializer.Serialize query_response

  // the response_msg needs to be converted to a ByteSegment
  let byte_response =
    json_response_str
    |> System.Text.Encoding.ASCII.GetBytes
    |> ByteSegment

  byte_response

// APIs
let user_registeration (json_request_obj) =
  printfn "User Registeration API called"

  let user_id = json_request_obj.User_id
  let pass_word = json_request_obj.Pass_word

  if user_registered_(user_id) then
    let my_feed_obj = Obj_for_Response(json_request_obj.Job, "0", "User is already registered. Try to register with a different username and password", _response_data)
    my_feed_obj
  else
      dict_users_table.Add(user_id, pass_word) 
      dict_logged_in.Add(user_id, true) 
      dict_newsfeed_table.Add(user_id, new List<string>())
      dict_followers_table.Add(user_id, new List<string>())
      dict_following_table.Add(user_id, new List<string>())

      let my_feed_obj = Obj_for_Response(json_request_obj.Job, "1", "User Registration done succesfully!!", _response_data)
      my_feed_obj

let login_user (json_request_obj) =
  printfn "Login User API called"

  let user_id = json_request_obj.User_id
  let pass_word = json_request_obj.Pass_word

  if not (user_registered_(user_id)) then
    let my_feed_obj = Obj_for_Response(json_request_obj.Job, "0", "User could not be registered. Please try again.", _response_data)
    my_feed_obj
  else
    if validate_password(user_id, pass_word) then
      dict_logged_in.[user_id] <- true

      let my_feed_obj = Obj_for_Response(json_request_obj.Job, "1", "User has been logged in successfully", _response_data)
      my_feed_obj
    else
      let my_feed_obj = Obj_for_Response(json_request_obj.Job, "0", "The password entered is invalid. Please try again with a valid password.", _response_data)
      my_feed_obj

let log_out_user (json_request_obj) =
  printfn "Logout User API called"

  let user_id = json_request_obj.User_id

  if not (dict_logged_in.[user_id]) then
    let my_feed_obj = Obj_for_Response(json_request_obj.Job, "0", "Current user has logged out", _response_data)
    my_feed_obj
  else
    dict_logged_in.[user_id] <- false
    let my_feed_obj = Obj_for_Response(json_request_obj.Job, "1", "User has been logout successfully", _response_data)
    my_feed_obj

let subscribe_to_user (json_request_obj) =
  printfn "Subscribe API called"

  let user_id = json_request_obj.User_id
  let subs_User_id = json_request_obj.Target_user_id

  if not (user_registered_(subs_User_id)) then
    let my_feed_obj = Obj_for_Response(json_request_obj.Job, "0", "The end user has not been registered in tthe system and hence could not be found", _response_data)
    my_feed_obj
  else
    dict_followers_table.[subs_User_id].Add(user_id)
    dict_following_table.[user_id].Add(subs_User_id)

    let responseMsg = "You are now following " + subs_User_id
    let following_list = dict_following_table.[user_id]

    let my_feed_obj = Obj_for_Response(json_request_obj.Job, "1", responseMsg, following_list)
    my_feed_obj

let fetch_user_feed (json_request_obj) =
  printfn "Fetch User Feed API called"

  let user_id = json_request_obj.User_id
  let newsFeed = dict_newsfeed_table.[user_id]

  let my_feed_obj = Obj_for_Response(json_request_obj.Job, "1", "User TimeLine displayed successfully", newsFeed)
  my_feed_obj

let fetch_followers (json_request_obj) =
  printfn "Fetch Followers API called"

  let user_id = json_request_obj.User_id
  let followers_list = dict_followers_table.[user_id]

  let my_feed_obj = Obj_for_Response(json_request_obj.Job, "1", "Displaying the user Follower list successfully", followers_list)
  my_feed_obj

let fetch_following_users (json_request_obj) =
  printfn "Fetch Following API called"

  let user_id = json_request_obj.User_id
  let following_list = dict_following_table.[user_id]

  let my_feed_obj = Obj_for_Response(json_request_obj.Job, "1", "Displaying the user Following list Successfully", following_list)
  my_feed_obj

let send_a_tweet (json_request_obj) =
  printfn "Tweet API called"

  let user_id = json_request_obj.User_id
  let tweeted_tweet = json_request_obj.Tweet

  if (not (user_logged_in_(user_id))) then
    let my_feed_obj = Obj_for_Response(json_request_obj.Job, "0", "User needs to be logged in to perform further operations.", _response_data)
    my_feed_obj
  else
    let actual_tweet = user_id + ": " + tweeted_tweet
    processing_tweets(actual_tweet)

    dict_newsfeed_table.[user_id].Add(actual_tweet)

    let followers_list = dict_followers_table.[user_id]
    for ii in followers_list do
        dict_newsfeed_table.[ii].Add(actual_tweet)

    let personal_feed = dict_newsfeed_table.[user_id]
    let my_feed_obj = Obj_for_Response(json_request_obj.Job, "1", "User has tweeted successfully", personal_feed)
    my_feed_obj


let tweet_retweet (json_request_obj) = 
  printfn "Retweet API called"

  let user_id = json_request_obj.User_id
  let tweeted_tweet = json_request_obj.Tweet

  let retweet = tweeted_tweet + "-- Retweeted by " + user_id
  dict_newsfeed_table.[user_id].Add(retweet)

  let followers_list = dict_followers_table.[user_id]
  for user in followers_list do
      dict_newsfeed_table.[user].Add(retweet)

  let personal_feed = dict_newsfeed_table.[user_id]
  let my_feed_obj = Obj_for_Response(json_request_obj.Job, "1", "Tweet has been retweeted successfully", personal_feed)

  my_feed_obj

let hashtag_querying (json_request_obj) = 
  printfn "Hashtag Querying API called"

  let search_hashtag = json_request_obj.Hashtag

  if (dict_hashtags_table.ContainsKey(search_hashtag)) then
    let hashtag_query_result = dict_hashtags_table.[search_hashtag]
    let my_feed_obj = Obj_for_Response(json_request_obj.Job, "1", "Search for Hashtag in tweets done successfully", hashtag_query_result)
    my_feed_obj
  else
    let my_feed_obj = Obj_for_Response(json_request_obj.Job, "0", "Please try with a different hashtag as no tweets available for this keyword", _response_data)
    my_feed_obj

let mention_querying (json_request_obj) =
  printfn "Mention Querying API called"

  let search_mention = json_request_obj.Mention

  if (dict_mentions_table.ContainsKey(search_mention)) then
    let mention_query_result = dict_mentions_table.[search_mention]
    let my_feed_obj = Obj_for_Response(json_request_obj.Job, "1", "Search for Mention in tweets done successfully", mention_query_result)
    my_feed_obj
  else
    let my_feed_obj = Obj_for_Response(json_request_obj.Job, "0", "Please try with a different mention as no tweets available for this keyword", _response_data)
    my_feed_obj

//Web Socket handler
let ws (webSocket : WebSocket) (context: HttpContext) =
  socket {
    // if socket loop is set to false, the server will stop receiving messages
    let mutable socket_loop = true

    while socket_loop do
      // the server will wait for a message to be received without blocking the thread
      let! socket_message = webSocket.read()

      match socket_message with
      | (Text, data_msg , true) ->        
        let string_msg = UTF8.toString data_msg 
        let Request_Obj = JsonConvert.DeserializeObject<Obj_for_Request>(string_msg)
        let mutable response_msg = Obj_for_Response(_response_job, _response_status, _response_msg, _response_data)

        if not (dict_webSocket_map.ContainsKey(Request_Obj.User_id)) then
          dict_webSocket_map.[Request_Obj.User_id] <- webSocket
        // The object will call the function based on the messages received 
        match Request_Obj.Job with
        | "user_registeration" -> response_msg <- user_registeration(Request_Obj)
        | "login_user" -> response_msg <- login_user(Request_Obj)
        | "subscribe_to_user" -> response_msg <- subscribe_to_user(Request_Obj)
        | "fetch_user_feed" -> response_msg <- fetch_user_feed(Request_Obj)
        | "fetch_followers" -> response_msg <- fetch_followers(Request_Obj)
        | "fetch_following_users" -> response_msg <- fetch_following_users(Request_Obj)
        | "send_a_tweet" -> response_msg <- send_a_tweet(Request_Obj)
        | "tweet_retweet" -> response_msg <- tweet_retweet(Request_Obj)
        | "hashtag_querying" -> response_msg <- hashtag_querying(Request_Obj)
        | "mention_querying" -> response_msg <- mention_querying(Request_Obj)
        | "log_out_user" -> response_msg <- log_out_user(Request_Obj)
        |_ -> printfn "Unmatched"

        if (Request_Obj.Job = "subscribe_to_user" && response_msg.ResponseStatus = "1") then
          let targetUserFollowersList = dict_followers_table.[Request_Obj.Target_user_id]
          let resp_msg = Request_Obj.User_id + " followed you"
          let targetUserResponse = Obj_for_Response("Followers", "1", resp_msg, targetUserFollowersList)
          let byteResp = serialization_byte(targetUserResponse)
          do! dict_webSocket_map.[Request_Obj.Target_user_id].send Text byteResp true

        if ((Request_Obj.Job = "send_a_tweet" || Request_Obj.Job = "tweet_retweet") && response_msg.ResponseStatus = "1") then
          let followers_list = dict_followers_table.[Request_Obj.User_id]
          let mutable feed_resp = Obj_for_Response(_response_job, _response_status, _response_msg, _response_data)

          for user in followers_list do
            // get user's websocket from map and send the tweet
            feed_resp <- feed_fetcher(user)

            if (feed_resp.ResponseStatus = "1") then
              let byte_Resp = serialization_byte(feed_resp)

              // the `send` function sends a message back to the client
              do! dict_webSocket_map.[user].send Text byte_Resp true
        
        // unicast
        let byt_response = serialization_byte(response_msg)
        do! webSocket.send Text byt_response true

      | (Close, _, _) ->
        let empty_response = [||] |> ByteSegment
        do! webSocket.send Close empty_response true

        // after sending a Close message, stop the socket loop
        socket_loop <- false

      | _ -> ()
    }

let app : WebPart = 
  choose [
    path "/websocket" >=> handShake ws
    GET >=> choose [ path "/twitter" >=> file "index.html"; browseHome]
    NOT_FOUND "No Handler Available." ]
    
[<EntryPoint>]
let main _ =
  startWebServer { defaultConfig with logger = Targets.create Verbose [||] } app
  0
