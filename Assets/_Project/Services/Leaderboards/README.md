# STREAM ON UGS Leaderboards

The four `.lb` files in this folder are scene-independent Unity Gaming Services
leaderboard configurations. Deploy them to the `production` environment from
`Window > Deployment`, then enable `Use Online Leaderboard` on
`RunnerCampaignSettings`.

- Runner, Tile Arena, and Plastic Knightmare use `keepBest` with descending scores.
- Followers use `keepLatest` because this board represents the player's current
  follower count, including decreases.
- Runtime authentication is anonymous and cached by the UGS Authentication SDK.
- Failed submissions remain in the WebGL persistent data queue and retry when the
  leaderboard provider is available again.
