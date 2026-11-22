# Spec: Home news receiver
Objective: opening Home with developer announcements, changelog and update notices, retained offline and received by upgraded copies while running. Existing no-game-execution/privacy boundaries remain.
Assumptions: public text feed, not a new account/push service; no external publication in this task. The board address is built in (developer, 2026-11-22): https://raw.githubusercontent.com/raiinman/Flutter-Deck/main/news/news.xml (`NewsFeed.DefaultEndpoint`). publisher-feed.txt beside the launcher is an optional override; absent or blank means the built-in address. Home first, Scaling second, Library third.
Build: powershell -NoProfile -File .\build.ps1
Test: powershell -NoProfile -File .\check-news.ps1
Structure: NewsFeed.cs pure parser/store/HTTPS receiver; Program.cs native Home; NewsPureTest.cs fixtures; publisher-feed.txt optional address override; news-example.xml the bundled notes shown until the first successful refresh (the 0.4.1 text by the changelog session).
Style: existing C#5, e.g. public static List<NewsItem> Parse(string xml). No external packages.
Success: bounded typed plain content; DTD/duplicate ID/bad timestamps/oversize rejected; changed item unread; source-separated bounded cache/read state; manual and 15-minute refresh only while running; one request at a time; 8-second total abort; no redirects/cookies/credentials/private data. Startup shows cached or bundled clearly labeled content. Home startup does not force setup; setup remains explicit.
Always preserve source/data and test. Ask before publishing endpoint or adding push service. Never execute feed content, fetch remote images, collect analytics, install updates, or claim old clients already receive.
Verification: pure fixture/stream/store tests and compiler; network delivery/GUI remain unverified. Endpoint deployment is separate required rollout step.
