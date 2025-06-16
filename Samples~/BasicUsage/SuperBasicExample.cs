using System.Collections.Generic;
using LoopKit;
using UnityEngine;

/*
    This is a super basic example of how to use the LoopKit SDK by using the LoopKitManager to emit events!
 */

public class SuperBasicExample : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // track is the most basic way to track an event or action in the game
        LoopKitManager.Instance.Track(
            "test_started",
            new Dictionary<string, object> { { "message", "Welcome to Loopkit!" } }
        );

        // identify the user (can be a user, player, customer, etc.) where the player is identified by a unique id
        LoopKitManager.Instance.Identify(
            "user_123",
            new Dictionary<string, object>
            {
                { "email", "test@loopkit.ai" },
                { "name", "Test User" },
                { "plan", "free" },
            }
        );

        // group is a way to group users together for analytics and reporting as a single entity  (can be a company, team, faction, etc.) where the player belongs to a group
        LoopKitManager.Instance.Group(
            "guild_456",
            new Dictionary<string, object> { { "name", "Test Guild" }, { "role", "member" } },
            "guild"
        );
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            LoopKitManager.Instance.Track(
                "space_pressed",
                new Dictionary<string, object> { { "message", "Space was pressed" } }
            );
        }
    }
}
