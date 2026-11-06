# Remote access

Use Huddle from your phone or another computer, when you choose to. For step-by-step help, see the
[User Guide](../../Huddle.UserGuide.md#using-huddle-from-another-device).

[Back to Features & Capabilities](../Features-&-Capabilities.md)

The first part of this page is a quick overview. For rules, limits and fixes, see
[In more detail](#in-more-detail).

## Start safe

**Strict local** is on by default: only the computer running Huddle can use it, with no sign-in. Other
devices are refused until you turn it off in **Settings > Remote access** and restart Huddle.

## Choose how to connect

| Option | What it's like |
| --- | --- |
| **Tailscale Serve** | Recommended. Encrypted and private to your Tailscale account, with no token prompt |
| **Local network** | Set up with a QR link. Works on that network only, and Huddle labels it downgraded because it is unencrypted |
| **Tailscale Funnel** | A public internet link that needs a password. The **Security check** flags it as critical |

## Control who gets in

- **Token and approval.** A device presents the gateway token and you approve it, once per device. A
  device on Tailscale Serve joins as **Limited** without the token, because Tailscale has already checked
  who you are.
- **Limited or Admin.** A **Limited** device can read and write in Rooms and Tasks, and can't change
  Settings. It can use **Request admin**.
- **Devices list.** **Rotate**, **Revoke** or **Remove** any device.
- **Security check.** Lists what is risky about your current setup, each with the fix.
- **Rate limits and trusted proxies.** Both are available for more advanced setups.

## If you're locked out

The `huddle auth-token` and `huddle devices` commands work from the computer running Huddle. See
[If you are locked out](../../Huddle.UserGuide.md#if-you-are-locked-out).

## Good to know

- Avoid the local network option on shared office or café Wi-Fi, because its traffic is unencrypted.
- While remote access is on, **Open** actions and the Library's **Open** and **Reveal** are hidden.
- Huddle automatically deletes loopback- and bootstrap-paired devices unseen for 30 days.

## In more detail

### Connecting with Tailscale

1. Install Tailscale on this computer and the other device, and sign in to the same account on both.
2. In the Tailscale admin page, turn on **MagicDNS** and **HTTPS Certificates**.
3. In **Settings > Remote access**, make sure **Strict local** is off, set **Tailscale** to **Serve**, and save.
4. Restart Huddle. The Gateway card shows a green **Running** chip and a link ending in `ts.net`.
5. Press **Copy link** and open the link on the other device.

### Connecting on your local network

1. In **Settings > Remote access**, make sure **Strict local** is off, set **Bind** to **lan**, and save.
2. Restart Huddle. If the firewall asks, allow **Private networks**.
3. Under **Pair a device**, scan the QR code or type the link beneath it.
4. The device shows "Waiting for approval". On this computer, choose its access level and press **Approve**.

This works on that network only, and the computer's address can change. Reserve the address on your router, or use
`http://<computer-name>.local:<port>`.

An installed copy of Huddle picks its own port. Before you choose a bind other than loopback, set `Team:Gateway:Port` to
a fixed port such as `5101`. Without it, Huddle starts on this computer only and says why.

### Links and requests

| Item | Rule |
| --- | --- |
| A setup link | Works once, for 10 minutes. **New link** makes another |
| An approval request | Waits 5 minutes. Rejecting one doesn't ban the device |
| A request from another Tailscale account | Marked **Shared user**, so check it before approving |
| Access you grant | Never more than you have |

### Managing devices

The **Devices** list shows each device's name, **Limited** or **Admin**, how it was paired, where it last connected from
and when it was last seen. This computer is marked **This device**.

| Action | Result |
| --- | --- |
| **Rotate** | Gives the device a new credential |
| **Revoke** | Ends its access at once, even in a Room that's streaming |
| **Remove** | Forgets the device |

**Request admin** on a **Limited** device's Settings page sends a request to a device that's already an admin.

With **Strict local** off, the browser on this computer is also a device. Huddle's launcher signs it in, and another
browser on this computer asks for the token.

### Reading the Security check

It sits at the bottom of **Remote access**, and the goal is an empty list. A **lan** bind is flagged as unencrypted, and
**Funnel** as critical.

### Lock-out commands

Run these on the computer running Huddle, in a plain PowerShell window.

| To | Run |
| --- | --- |
| Read the gateway token | `huddle auth-token --show`, which won't print into a file or pipe |
| Replace the token | `huddle auth-token --rotate`, then restart Huddle |
| Open Huddle here, already signed in | `huddle dashboard` |
| List devices and waiting requests | `huddle devices list` |
| Approve or reject a request | `huddle devices approve <request id>` or `huddle devices reject <request id>` |
| Revoke a device | `huddle devices revoke --device <device id>` |

### Housekeeping

Every 60 seconds, Huddle deletes the unseen devices noted above and clears spent rate-limit entries, expired sessions and
expired pairing requests.
