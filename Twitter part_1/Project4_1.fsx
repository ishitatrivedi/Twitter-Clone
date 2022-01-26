#time "on"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"
open System
open Akka
open Akka.Actor
open System.Collections.Generic
open Akka.FSharp

let system = System.create "system" (Configuration.defaultConfig())
let mutable clientNo = fsi.CommandLineArgs.[1] |> int
let mutable isCheck = false
let mutable ranFollUsrId = 2
let mutable countTotalfollower = 0
let mutable terminationCounter = 0
if clientNo < 1000 then 
    clientNo<-1000
let lstTweet = new List<String>()
for i = 1 to 10 do
    lstTweet.Add(sprintf "Tweeting randomly #tweet%i by @User%i related to course COP5615" i i )
let mutable tweet_engine = null
let mutable actor_simulator = null
let mutable tweet_distributor = null

type structTweet = struct
    val tweetId: int
    val a_tweet: String
    val tweetCreator: int
    new (twtId,msg,creator) = {
        
        tweetId = twtId; 
        a_tweet = msg; 
        tweetCreator = creator;
    }
end

type structRetweet = struct
    val retwtId: int
    val tweetId: int
    val userSharedBy: int
    new (rtId,twtId,usrSharedId) = {
        retwtId = rtId;
        tweetId = twtId;
        userSharedBy = usrSharedId
    }
end

type structTweetUserDetails = struct
    val a_tweet: String
    val tweetId: int
    val isRetweet: bool
    val retwtId: int
    val tweetCreator: int
    val userSharedBy: int
    new (twtId,twtMsg,creatorId,isRetwt,retwtId,sharedId) = {
        
        tweetId= twtId;
        a_tweet = twtMsg;
        tweetCreator = creatorId;
        isRetweet = isRetwt;
        retwtId = retwtId;
        userSharedBy = sharedId
    }
end

type simulationEngineMsg = 
    | Init of int
    | SpawningOfClients of int
    | PopulateFollowerFollowing of Map<int,IActorRef>
    | TweetSimulation of String*int
    | TweetSimAll of String
    | RetweetSimulation of int*int
    | RetweetSimByAll 
    | SimStateSwitch of int
    | GetAllTweetCount of int
    | SimListFollowersClient of int
    | SimListFollowerServer of int
    | SimListFollowingClient of int
    | SimListFollowingServer of int
    | ShowHomeTimeline of int
    | LookupTweetFromClient of String*int
    | ResetSystem

type twitterCloneEngineMsg= 
    | Initialization of int
    | ClientRegistration of int*IActorRef
    | PopulateUser of Map<int,Set<int>>*Map<int,Set<int>>*Set<int>
    | TweetRegistration of String*int
    | ReTweetRegistration of int*int
    | ServerOffOn of int
    | DisplayServer of String
    | OutputOfFollowers of int
    | OutputOfFollowings of int
    | WorkComplete

type distributionEngineMsg = 
    | TweetDistribute of structTweetUserDetails*Set<IActorRef>

type userMsg = 
    | InitializeUser of int
    | GenTweet of String
    | GenRetweet of int
    | RegisterTweet of structTweetUserDetails
    | PopulateFollowers of Set<int>
    | AppendFollower of int
    | OutputFollowers
    | PopulateFollowing of Set<int>
    | AppendFollowing of int
    | OutputFollowing
    | SaveTweetInTimeline of structTweetUserDetails
    | RandomRetweet
    | UserLogin
    | UserLogout
    | AddToHomeTimeline of Set<structTweetUserDetails>
    | OutputUserHomeTimeline
    | Output of Set<structTweetUserDetails>
    | SearchItem of String

let UserActor (mailbox : Actor<_>) =
    let mutable currentId = 0 
    let mutable isUserActive = true
    let mutable setFollowers = Set.empty<int>
    let mutable setFollowing = Set.empty<int>
    let mutable lstHomeTimelineTwt = new List<structTweetUserDetails>()
    let mutable lstUserTimelineTwt = new List<structTweetUserDetails>()
    let objRandom = Random()
    let rec loop() = actor {
        
        let! strMsg = mailbox.Receive()
        match strMsg with
        | InitializeUser (id)->
            currentId<-id
        | UserLogin ->
            tweet_engine<!ServerOffOn currentId
            isUserActive<-true
        | UserLogout ->
            tweet_engine<!ServerOffOn currentId
            isUserActive<-false    
        | GenTweet (a_tweet)->
            tweet_engine<!TweetRegistration (a_tweet,currentId)
        | GenRetweet (tweetId) -> 
            tweet_engine<!ReTweetRegistration (tweetId,currentId)
        | RegisterTweet (a_tweet) ->
            lstUserTimelineTwt.Add(a_tweet)
        | RandomRetweet ->
            let mutable a_tweet = objRandom.Next(0,lstHomeTimelineTwt.Count)
            mailbox.Self<!GenRetweet lstHomeTimelineTwt.[a_tweet].tweetId
        | PopulateFollowers(setFlwr) ->
            setFollowers<-setFlwr
        | AppendFollower (follower) ->
            setFollowers<-setFollowers.Add(follower)
        | PopulateFollowing (setFlwng) ->
            setFollowing<-setFlwng
        | AppendFollowing (userFollowing) ->
            setFollowing<-setFollowing.Add(userFollowing)
        | SaveTweetInTimeline a_tweet ->
            if a_tweet.isRetweet then 
                actor_simulator<!GetAllTweetCount 2
            else 
                actor_simulator<!GetAllTweetCount 1
            lstHomeTimelineTwt.Add(a_tweet)
        | AddToHomeTimeline set ->
            for a_tweet in set do 
                lstHomeTimelineTwt.Add(a_tweet)
        | Output setSearchResult ->
            printfn "The total number of search results found are %i" setSearchResult.Count
            if setSearchResult.Count > 0 then
                printfn "Displaying top 10 results out of all the search results below -"
                printfn " "
                let mutable i = 0
                for a_tweet in setSearchResult do
                    if i < 10 then
                        i<-i+1
                        printfn "Id of Tweet - %i" a_tweet.tweetId
                        printfn "Tweet Data - %s" a_tweet.a_tweet
                        printfn "Tweet Created By - User%i"  a_tweet.tweetCreator
                        if a_tweet.isRetweet then 
                            printfn "Tweet has been retweeted by User%i" a_tweet.userSharedBy
                        printfn " "       
        | OutputFollowers ->
            for userFollower in setFollowers do 
                printf "%i, " userFollower
            printfn " "
            let lstTmpFollower = new List<int>(setFollowers)
            let randomFollower = lstTmpFollower.[objRandom.Next(0,lstTmpFollower.Count)]
            ranFollUsrId<-randomFollower
            terminationCounter<-terminationCounter + 1
            printfn "User %i is selected as the random follower." randomFollower
        | OutputFollowing ->
            printfn "Users following are -"
            for userFollowing in setFollowing do 
                printf "%i, " userFollowing
            printfn " "
        | OutputUserHomeTimeline ->
            printfn "  "
            for a_tweet = lstHomeTimelineTwt.Count-1 downto Math.Max(lstHomeTimelineTwt.Count-11,0) do 
                printfn "Tweet Id : %i" lstHomeTimelineTwt.[a_tweet].tweetId
                printfn "Tweet Text : %s" lstHomeTimelineTwt.[a_tweet].a_tweet
                printfn "Tweet Creator : User%i"  lstHomeTimelineTwt.[a_tweet].tweetCreator
                if lstHomeTimelineTwt.[a_tweet].isRetweet then 
                    printfn "Tweet has been retweeted by User%i" lstHomeTimelineTwt.[a_tweet].userSharedBy
                printfn "  "
        | SearchItem item ->
            tweet_engine<!DisplayServer (item)
        return! loop()
    }
    loop()

let simulation_engine (mailbox : Actor<_>) = 

    let mutable mapSimFollower = Map.empty<int,Set<int>>
    let mutable mapSimFollowing = Map.empty<int,Set<int>>
    let mutable lstSimIsActive = new List<bool>()
    let mutable mapSimUser = Map.empty<int,IActorRef> 
    let mutable setFamousUsers = Set.empty<int>
    let objRandom = Random()
    let mutable clientNo = 0
    let mutable constZipf = 0.0
    let mutable countTweetByAll = 0
    let rec loop() = actor {
        
        let! strMsg = mailbox.Receive()
        match strMsg with
        | Init (clientNum)->
            lstSimIsActive.Add(true)
            clientNo<-clientNum
            let mutable c = 0.0
            for i = 1 to clientNo do
                c<-c + (1.0/(i|> float))
            constZipf<-1.0/c
            for i = 1 to clientNo do 
                lstSimIsActive.Add(true)
                mapSimFollower<-mapSimFollower.Add(i,Set.empty)
                mapSimFollowing<-mapSimFollowing.Add(i,Set.empty)

            tweet_engine<!Initialization clientNo
            //client_generator<!SpawningOfClients clientNo
            actor_simulator<!SpawningOfClients clientNo

        |SpawningOfClients (clientNum) ->
            for user_id = 1 to clientNum do 
                let client = spawn system (sprintf "User%i" user_id) UserActor
                client<! InitializeUser user_id
                tweet_engine<!ClientRegistration (user_id,client)
            tweet_engine<!WorkComplete

        | PopulateFollowerFollowing (mapUser) ->
            mapSimUser<-mapUser
            let famousUserNo = (10*clientNo)/100
            while (setFamousUsers.Count < famousUserNo) do 
                setFamousUsers<-setFamousUsers.Add(objRandom.Next(1,clientNo+1))          
            
            let addFollowers (userId,countFollowers) =    
                while (mapSimFollower.[userId].Count <= countFollowers) do 
                    let follower = objRandom.Next(1,clientNo+1)
                    if(not(setFamousUsers.Contains(follower)) && follower <> userId) then 
                        mapSimFollower<-mapSimFollower.Add(userId,mapSimFollower.[userId].Add(follower))
                        mapSimFollowing<-mapSimFollowing.Add(follower,mapSimFollowing.[follower].Add(userId))
                mapSimUser.[userId]<!PopulateFollowers mapSimFollower.[userId]                

            let addFollowingOfFamousUsers = 
                let mutable lstFamousUser = new List<int>(setFamousUsers)
                for famousUser in setFamousUsers do 
                    let intUserCount = setFamousUsers.Count
                    let followingcount = objRandom.Next((intUserCount*10)/100,(intUserCount*20)/100)
                    while(mapSimFollowing.[famousUser].Count < followingcount) do 
                        let mutable famousUserToFollow = lstFamousUser.[objRandom.Next(0,intUserCount)]
                        if(famousUserToFollow <> famousUser) then 
                            mapSimFollowing<-mapSimFollowing.Add(famousUser,mapSimFollowing.[famousUser].Add(famousUserToFollow))
                            mapSimUser.[famousUserToFollow]<!AppendFollower famousUser
                            mapSimFollower<-mapSimFollower.Add(famousUserToFollow,mapSimFollower.[famousUserToFollow].Add(famousUser))    
            
            let famousUserFollowerCount = (constZipf*(clientNo|>float)) |>int
            let followerLowerLmt = Math.Max(famousUserFollowerCount - (famousUserFollowerCount*15)/100,0)
            let followerUpperLmt = famousUserFollowerCount + (famousUserFollowerCount*15)/100
            for famousUser in setFamousUsers do 
                addFollowers (famousUser,objRandom.Next(followerLowerLmt,followerUpperLmt))

            for userId = 1 to clientNo do 
                if not (setFamousUsers.Contains(userId)) then
                    let followerCount =  ((constZipf*(clientNo|>float))|> int)/objRandom.Next(2,10) + 1
                    let lowerLmt = Math.Max(followerCount - (followerCount*15)/100,0)
                    let upperLmt = followerCount + (followerCount*15)/100
                    addFollowers (userId,objRandom.Next(lowerLmt,upperLmt))
            addFollowingOfFamousUsers
            for user in mapSimUser do 
                user.Value<!PopulateFollowing mapSimFollowing.[user.Key]
            for user in mapSimFollower do 
                countTotalfollower<-countTotalfollower + user.Value.Count
            tweet_engine<!PopulateUser (mapSimFollower,mapSimFollowing,setFamousUsers)
            Threading.Thread.Sleep(110)
            terminationCounter<-terminationCounter + 1 

        | TweetSimulation (a_tweet,userId) ->
            //printfn "User%i tweeted %s" userId a_tweet
            mapSimUser.[userId]<!GenTweet a_tweet
        | RetweetSimulation (tweetId,userId)->
            mapSimUser.[userId]<!GenRetweet tweetId
        | LookupTweetFromClient (item,userId)->
            mapSimUser.[userId]<!SearchItem item
        | TweetSimAll (a_tweet) ->
            for i = 1 to clientNo do 
                mapSimUser.[i]<!GenTweet a_tweet
        | GetAllTweetCount (testId)->
            countTweetByAll<-countTweetByAll + 1
            if testId = 1 then 
                if countTweetByAll >= 10* countTotalfollower-1 then 
                    isCheck<-true
            else if testId = 2 then 
                if countTweetByAll >= countTotalfollower then 
                    isCheck<-true
        | ResetSystem -> 
            countTweetByAll<-0
        | RetweetSimByAll ->
            for i = 1 to clientNo do 
                mapSimUser.[i]<!RandomRetweet
        | SimListFollowersClient userId ->
            mapSimUser.[userId]<!OutputFollowers
        | SimListFollowingClient userId ->
            mapSimUser.[userId]<!OutputFollowing
        | SimListFollowerServer userId ->
            tweet_engine<!OutputOfFollowers userId
        | SimListFollowingServer userId ->
            tweet_engine<!OutputOfFollowings userId
        | SimStateSwitch user ->
            if lstSimIsActive.[user] then 
                mapSimUser.[user]<!UserLogout
            else 
                mapSimUser.[user]<!UserLogin
            lstSimIsActive.[user]<-not lstSimIsActive.[user] 
        | ShowHomeTimeline user ->
            mapSimUser.[user]<!OutputUserHomeTimeline
        return! loop()
    }
    loop()

let twitterClone_engine (mailbox : Actor<_>) =
    let mutable userlist_map = Map.empty<int,IActorRef> 
    let mutable mapFollower = Map.empty<int,Set<int>>
    let mutable mapFollowing = Map.empty<int,Set<int>>
    let mutable active_client_List = new List<bool>()
    let mutable setOfFamousUsers = Set.empty<int>
    let mutable mapKeywords = Map.empty<String,Set<int>> 
    let mutable mapMentions = Map.empty<String,Set<int>> 
    let mutable mapHashtags = Map.empty<String,Set<int>>
    let mutable mapPendingTweets = Map.empty<int,Set<structTweetUserDetails>>
    let mutable mapOfTweets = Map.empty<int,structTweet>
    let mutable mapOfRetweets = Map.empty<int,structRetweet>
    let mutable boolTweetID = 1 
    let mutable boolRetweetID = -1
    let mutable intClientTotal = 0 
    
    let createFollowerRefMap tweetCreator structTweet=
        let mutable setOfFollowers = mapFollower.[tweetCreator]
        let mutable setFollowerRef = Set.empty<IActorRef>
        for followerUser in setOfFollowers do 
            if active_client_List.[followerUser] = true then 
                setFollowerRef<-setFollowerRef.Add(userlist_map.[followerUser])
            else
                mapPendingTweets<-mapPendingTweets.Add(followerUser,mapPendingTweets.[followerUser].Add(structTweet))
        setFollowerRef

    let findingofTags (a_tweet:String) booloftweetID = 
        let lstKeyword = new List<String>()
        let lstMention = new List<String>()
        let lstHashtag = new List<String>()
        let populatingAllMaps (lstTweetMsg:List<String>) _map =
            let mutable mapping = Map.empty<String,Set<int>>
            if _map = 1 then 
                mapping<-mapKeywords
            else if _map = 2 then 
                mapping<-mapMentions
            else 
                mapping<-mapHashtags 
            for aword in lstTweetMsg do
                if (mapping.ContainsKey(aword)) then 
                    mapping<-mapping.Add(aword,mapping.[aword].Add(booloftweetID))
                else
                    let set = Set.empty<int>.Add(booloftweetID)
                    mapping<-mapping.Add(aword,set)
            if _map = 1 then 
                mapKeywords<-mapping
            else if _map = 2 then 
                mapMentions<-mapping
            else 
                mapHashtags<-mapping
        let ifBreak = a_tweet.Split ' '      
        for aword in ifBreak do
            if (aword <> "") then
                if(aword.[0] = '@') then 
                    lstMention.Add(aword)
                else if(aword.[0] = '#') then 
                    lstHashtag.Add(aword)
                else
                    lstKeyword.Add(aword)
        populatingAllMaps lstKeyword 1
        populatingAllMaps lstMention 2
        populatingAllMaps lstHashtag 3

    let rec loop() = actor {
        let! msg = mailbox.Receive()
        match msg with
        |Initialization (nofClients) ->
            intClientTotal<-nofClients
            active_client_List.Add(true)
            for i = 1 to intClientTotal do 
                let mutable setofTweets = Set.empty
                let mutable setofPending = Set.empty
                mapFollower<-mapFollower.Add(i,setofTweets)
                mapFollowing<-mapFollowing.Add(i,setofTweets)
                mapPendingTweets<-mapPendingTweets.Add(i,setofPending)
                active_client_List.Add(true)
        |ClientRegistration (clientId,actorRef)->
            userlist_map<-userlist_map.Add(clientId,actorRef)             
        |PopulateUser (followers,following,celebrity) -> 
            mapFollowing<-following
            mapFollower<-followers
            setOfFamousUsers<-celebrity
        |TweetRegistration (the_tweet,the_creator) ->
            mapOfTweets<-mapOfTweets.Add(boolTweetID,new structTweet(boolTweetID,the_tweet,the_creator))
            findingofTags the_tweet boolTweetID
            let storedTweet = new structTweetUserDetails(boolTweetID,the_tweet,the_creator,false,0,0)
            userlist_map.[the_creator]<!RegisterTweet storedTweet
            let TweetDistSet = createFollowerRefMap the_creator storedTweet
            tweet_distributor<!TweetDistribute (storedTweet, TweetDistSet)
            boolTweetID<-boolTweetID + 1    
        |ReTweetRegistration (tweetId,userSharedBy) ->
            mapOfRetweets<-mapOfRetweets.Add(boolRetweetID,new structRetweet(boolRetweetID,tweetId,userSharedBy))
            let tweetCreator = mapOfTweets.[tweetId].tweetCreator
            let a_tweet = mapOfTweets.[tweetId].a_tweet
            findingofTags a_tweet boolRetweetID
            let tweetStruct = new structTweetUserDetails(tweetId,a_tweet,tweetCreator,true,boolRetweetID,userSharedBy)
            userlist_map.[userSharedBy]<!RegisterTweet tweetStruct
            tweet_distributor<!TweetDistribute (tweetStruct,(createFollowerRefMap userSharedBy tweetStruct))
            boolRetweetID<-boolRetweetID - 1
        |DisplayServer symboltoSearch ->
            let createSetforDisplay (dispSet:Set<int>) = 
                let mutable displaytweetSet = Set.empty<structTweetUserDetails>
                for tweetId in dispSet do
                    if tweetId > 0 then
                        displaytweetSet<-displaytweetSet.Add(new structTweetUserDetails(tweetId,mapOfTweets.[tweetId].a_tweet,mapOfTweets.[tweetId].tweetCreator,false,0,0))
                displaytweetSet

            let mutable displaySet = Set.empty<structTweetUserDetails>
            if symboltoSearch.[0] = '@' && mapMentions.ContainsKey(symboltoSearch) then
                displaySet<-createSetforDisplay mapMentions.[symboltoSearch]  
            else if symboltoSearch.[0] = '#' && mapHashtags.ContainsKey(symboltoSearch) then
                displaySet<-createSetforDisplay mapHashtags.[symboltoSearch] 
            else if mapKeywords.ContainsKey(symboltoSearch) then
                displaySet<-createSetforDisplay mapKeywords.[symboltoSearch] 
            mailbox.Sender()<!Output displaySet
        |OutputOfFollowers id->
            printfn "Following is The list of followers of User %d:-" id
            for user in mapFollower.[id] do 
                printf "%i, " user
            printfn " "
        |OutputOfFollowings id->
            printfn "Following is the list of users following User %d:-"id
            for user in mapFollowing.[id] do 
                printf "%i, " user
            printfn " "
        |ServerOffOn userId ->
            active_client_List.[userId] <-(not active_client_List.[userId])
            if active_client_List.[userId] = true then 
                userlist_map.[userId]<!AddToHomeTimeline mapPendingTweets.[userId]
                mapPendingTweets<-mapPendingTweets.Add(userId,Set.empty) 
        |WorkComplete ->
            actor_simulator<!PopulateFollowerFollowing userlist_map      
        return! loop()
    }
    loop()

let distribution_engine (mailbox : Actor<_>) =
    let rec loop() = actor {
        let! msg = mailbox.Receive()
        match msg with
        | TweetDistribute (structTweetUserDetails,setOfFollowers) ->
            for a_user in setOfFollowers do 
                a_user<!SaveTweetInTimeline structTweetUserDetails            
        return! loop()
    }
    loop()

//Spawning all actors
tweet_engine<-spawn system "tweet_engine" twitterClone_engine
tweet_distributor<-spawn system "tweet_Distributor" distribution_engine
actor_simulator<-spawn system "actor_simulator" simulation_engine
actor_simulator<!Init (clientNo)
let objStopwatch = Diagnostics.Stopwatch.StartNew()

let mutable check = true
while terminationCounter = 0 do
    check<-false
printfn "Twitter Clone Engine has been created for %d users." clientNo
for i = 0 to 9 do 
    actor_simulator<!TweetSimAll lstTweet.[i]
while not isCheck do 
    check<-check
objStopwatch.Stop()
printfn "After all the tweets are sent and received in the system....."
printfn "%f ms is time taken to send and receive %i tweets in the system." objStopwatch.Elapsed.TotalMilliseconds (countTotalfollower*10) 
actor_simulator<!ResetSystem
isCheck<-false
objStopwatch.Reset()
objStopwatch.Start()
actor_simulator<!RetweetSimByAll 
while not isCheck do 
    check<-check
objStopwatch.Stop()
printfn "After all the retweets are sent and received in the system....."
printfn "%f ms is time taken to send and receive  %i retweets in the system." objStopwatch.Elapsed.TotalMilliseconds countTotalfollower 
printfn " "
let randomUser = Random().Next(1,clientNo)

printfn "Listing out the follower list of any random user - User%i." randomUser
actor_simulator<!SimListFollowersClient randomUser
Threading.Thread.Sleep(100)
while terminationCounter < 1 do 
    check<-false
//printfn "Listing out the users which are followed by User%i." ranFollUsrId
tweet_engine<!OutputOfFollowings ranFollUsrId
Threading.Thread.Sleep(100)
printfn " "
printfn "Disconnecting User%i by making it offline!" ranFollUsrId
printfn " "
actor_simulator<!SimStateSwitch ranFollUsrId
printfn "User%i sends a tweet which would not show up in User%i's home-timeline as it is offline." randomUser ranFollUsrId
printfn "Tweet sent is - This #RandomTweet won't be visible in User%i's timeline."ranFollUsrId
actor_simulator<!TweetSimulation (sprintf "This #RandomTweet won't be visible in User%i's timeline." ranFollUsrId,randomUser)
printfn " "
printfn "Displaying offline User%i's home-timeline" ranFollUsrId
actor_simulator<!ShowHomeTimeline ranFollUsrId
Threading.Thread.Sleep(500)
printfn " "
printfn "Connecting User%i by making it online!" ranFollUsrId
actor_simulator<!SimStateSwitch ranFollUsrId
Threading.Thread.Sleep(100)
printfn "Displaying online User%i's home-timeline" ranFollUsrId
actor_simulator<!ShowHomeTimeline ranFollUsrId
Threading.Thread.Sleep(500)
printfn " "
objStopwatch.Reset()
objStopwatch.Start()
printfn "Keyword Search - Searching for the keyword 'COP5615'"
actor_simulator<!LookupTweetFromClient ("COP5615", randomUser)
objStopwatch.Stop()
printfn "%f ms is the time taken to search for string - COP5615 in system" objStopwatch.Elapsed.TotalMilliseconds
Threading.Thread.Sleep(100)
printfn " "
objStopwatch.Reset()
objStopwatch.Start()
printfn "Mention Search - Searching for mention of the user @User2"
actor_simulator<!LookupTweetFromClient ("@User2", randomUser)
objStopwatch.Stop()
printfn "%f ms is time taken to search for mention of @User2 in system." objStopwatch.Elapsed.TotalMilliseconds
Threading.Thread.Sleep(100)
printfn " "
objStopwatch.Reset()
objStopwatch.Start()
printfn "Hashtag Search - Searching for the hashtag #tweet5"
actor_simulator<!LookupTweetFromClient ("#tweet5", randomUser)
objStopwatch.Stop()
printfn "%f ms is the time taken to search for #tweet5 in the system" objStopwatch.Elapsed.TotalMilliseconds
Threading.Thread.Sleep(100)
printfn " "
Threading.Thread.Sleep(2000)