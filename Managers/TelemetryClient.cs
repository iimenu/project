/*
 * ii Reborn
 * Copyright (C) 2026 @corgisolutions
 * Licensed under GNU GPL v3.0-or-later — see LICENSE and NOTICE.
 * Do not remove this notice.
 */

using GorillaNetworking;
using iiMenu.Classes.Menu;
using iiMenu.Extensions;
using iiMenu.Menu;
using iiMenu.Utilities;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Valve.Newtonsoft.Json.Linq;
using WebSocketSharp;

namespace iiMenu.Managers
{
    public class TelemetryClient : MonoBehaviour
    {
        public const bool Enabled = true;
        public static bool DisableTelemetry;

        private const string EndpointHost = "api-prod-iidk-de.corgi.st";

        public static string CurrentHost = EndpointHost;
        public static string WireEndpoint = "wss://" + EndpointHost + "/v2/ws";
        public static string ConfigEndpoint = "https://" + EndpointHost + "/v2/cfg";

        public const string MetadataStatusUrl = "https://github.com/iimenu/manifest/raw/refs/heads/main/menustatus.json";
        public const string MetadataVersionUrl = "https://github.com/iimenu/manifest/raw/refs/heads/main/menuversion.json";
        public const string MetadataApiUrl = "https://github.com/iimenu/manifest/raw/refs/heads/main/api.json";

        public const string SigningPublicKey = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEohKBpx0zIokiQbQ9MvN5as5rYSXUS/Lp7tqDpwbondDFYWVLGz31J47YQno/hgtF/gXyZDvdkmmB15X86H4nrw==";

        private const int AttestMaxAgeSec = 21600;
        private const int ClockSkewSec = 7200;

        private const float FlushInterval = 0.25f;
        private const float RoomStateDelay = 3f;
        private const float RigWaitTimeout = 10f;
        private const float CosmeticWaitTimeout = 5f;
        private const int MaxQueued = 256;

        private const float FallbackPollMin = 60f;
        private const float FallbackPollMax = 180f;
        private const float CfgPollKilled = 60f;
        private const float CfgSafetyNet = 900f;
        private const int FrameMax = 4096;

        public static bool OutdatedVersion;
        public static string LatestVersion;
        public static string UpdateDownloadUrl;
        public static string UpdateSha256;
        public static string UpdateReleaseUrl;

        private static TelemetryClient instance;

        private static readonly Queue<byte[]> Pending = new Queue<byte[]>();   // encoded frames: [type][varint len][payload]

        private static bool inRoom;
        private static string currentRoom;
        private static string currentRegion;
        private static bool currentRoomVisible;

        private static readonly Dictionary<NetPlayer, ushort> Sids = new Dictionary<NetPlayer, ushort>();
        private static ushort nextSid;

        private static float nextFlushTime;

        private static bool betaBuildWarned;

        private static readonly ConcurrentQueue<Action> mainThread = new ConcurrentQueue<Action>();

        private static readonly ConcurrentQueue<byte[]> sendQueue = new ConcurrentQueue<byte[]>();
        private static readonly object verifyLock = new object();
        private static Thread senderThread;
        private static volatile bool senderDraining;
        private static volatile WebSocket ws;
        private static bool wsHelloed;
        private static bool wsExpectingConfig;
        private static float wsExpectingConfigAt;
        private static float wsConnectingSince;
        private static float wsAckDeadline;
        private static float heartbeatInterval = 10f;
        private static float nextHeartbeatAt;
        private static float lastServerContact;
        private static float nextWsAttemptAt;
        private static float reconnectDelay;
        private static int connectFailures;
        private static bool evicted;
        private static int protocolKicks;
        private static bool killActive;
        private static bool primaryDown;
        private static bool bootstrapDone;
        private static bool shuttingDown;
        private static bool suppressReconnectOnce;
        private static bool unknownFrameChecked;
        private static float nextCfgPollAt;
        private static float nextFallbackPollAt;
        private static float nextSafetyNetAt;
        private static int heldRev;
        private static int endpointGeneration;

        private static bool attestOk;
        private static string attestDomain;
        private static DateTime attestTs;
        private static DateTime heldApiTs = DateTime.MinValue;
        private static ManagedEcdsa verifyKey;
        private static bool verifyBroken;
        private static string selfUid;
        private static string lastVerifyCanonical;
        private static string lastVerifySignature;
        private static bool lastVerifyResult;
        private static float attestRetryDelay;
        private static List<string> apiDomains = new List<string>();
        private static float nextApiRotateAt;
        private static int safetyNetTicks;

#if DEBUG
        private static void Dbg(string message) => LogManager.Log("[TelemetryClient] " + message);

        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty);
#endif

        private static byte[] installId;
        public static bool InstallIdFromMachine;

        private const string InstallIdSalt = "ii-Reborn-install-v2";

        // HELLO install_id: HMAC-SHA-256(key=salt, msg=MachineGuid), first 16 bytes; registry read fails -> random 16B in InstallId.txt, InstallIdFromMachine=0
        public static byte[] GetInstallId()
        {
            if (installId != null)
                return installId;

            string machineGuid = ReadMachineGuid();
            if (!string.IsNullOrEmpty(machineGuid))
            {
#if DEBUG
                Dbg($"machineguid string {machineGuid}");
                Dbg($"machineguid utf8 bytes {Hex(Encoding.UTF8.GetBytes(machineGuid))}");
#endif
                using HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(InstallIdSalt));
                installId = hmac.ComputeHash(Encoding.UTF8.GetBytes(machineGuid)).Take(16).ToArray();
                InstallIdFromMachine = true;
#if DEBUG
                Dbg($"install id machine derived {Hex(installId)}");
#endif
                return installId;
            }

#if DEBUG
            Dbg("machineguid read failed, using InstallId.txt fallback");
#endif
            string path = $"{PluginInfo.BaseDirectory}/InstallId.txt";
            string hex = File.Exists(path) ? File.ReadAllText(path).Trim() : null;

            if (string.IsNullOrEmpty(hex) || !Regex.IsMatch(hex, "^[0-9a-fA-F]{32}$"))
            {
                hex = Guid.NewGuid().ToString("N");
                File.WriteAllText(path, hex);
#if DEBUG
                Dbg($"generated new random install id in {path}");
#endif
            }

            installId = new byte[16];
            for (int i = 0; i < 16; i++)
                installId[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            InstallIdFromMachine = false;
#if DEBUG
            Dbg($"install id fallback {hex} fromMachine False");
#endif
            return installId;
        }

        private const uint RRF_RT_REG_SZ = 0x00000002;
        private static readonly UIntPtr HKeyLocalMachine = (UIntPtr)0x80000002u;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegGetValueW(UIntPtr hive, string subKey, string value, uint flags, uint type, byte[] data, ref uint size);

        private static string ReadMachineGuid()
        {
            try
            {
                byte[] buffer = new byte[128];
                uint size = (uint)buffer.Length;
                int result = RegGetValueW(HKeyLocalMachine, "SOFTWARE\\Microsoft\\Cryptography", "MachineGuid", RRF_RT_REG_SZ, 0, buffer, ref size);
#if DEBUG
                Dbg($"RegGetValueW result {result} size {size}");
                if (result == 0)
                    Dbg($"machineguid raw buffer {Hex(buffer)}");
#endif
                if (result != 0)
                    return null;

                return Encoding.Unicode.GetString(buffer, 0, (int)size).TrimEnd('\0');
            }
#if DEBUG
            catch (Exception e)
            {
                Dbg($"RegGetValueW threw {e.GetType().Name} {e.Message}");
                return null;
            }
#else
            catch
            {
                return null;
            }
#endif
        }

        private void Awake()
        {
            instance = this;
            shuttingDown = false;

            senderDraining = false;

            if (senderThread == null || !senderThread.IsAlive)
            {
                senderThread = new Thread(SenderLoop);
                senderThread.IsBackground = true;
                senderThread.Start();
            }
            evicted = false;
            protocolKicks = 0;
            connectFailures = 0;
            reconnectDelay = 0f;
            wsHelloed = false;
            wsExpectingConfig = false;
#if DEBUG
            Dbg("awake, subscribing network hooks");
#endif
            NetworkSystem.Instance.OnJoinedRoomEvent += OnJoinRoom;
            NetworkSystem.Instance.OnReturnedToSinglePlayer += OnLeaveRoom;
            NetworkSystem.Instance.OnPlayerJoined += OnPlayerJoin;
            NetworkSystem.Instance.OnPlayerLeft += OnPlayerLeave;
        }

        private void OnDestroy()
        {
            Shutdown();

#if DEBUG
            Dbg("ondestroy, unsubscribing network hooks");
#endif
            NetworkSystem.Instance.OnJoinedRoomEvent -= OnJoinRoom;
            NetworkSystem.Instance.OnReturnedToSinglePlayer -= OnLeaveRoom;
            NetworkSystem.Instance.OnPlayerJoined -= OnPlayerJoin;
            NetworkSystem.Instance.OnPlayerLeft -= OnPlayerLeave;
        }

        private void Update()
        {
            while (mainThread.TryDequeue(out Action action))
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    LogManager.LogError($"telemetry action failed: {e.Message}");
                }
            }

            float now = Time.unscaledTime;

            if (DisableTelemetry && WsBusy)
            {
                suppressReconnectOnce = true;
                try { ws.Close(1000, "optout"); } catch { }
            }

            if (killActive && now > nextCfgPollAt)
            {
                nextCfgPollAt = now + CfgPollKilled;
                StartCoroutine(FetchConfig());
            }
            else if (bootstrapDone && !killActive && !primaryDown && !WsOpen && now > nextCfgPollAt)
            {
                nextCfgPollAt = now + UnityEngine.Random.Range(FallbackPollMin, FallbackPollMax);
                StartCoroutine(FetchConfig());
            }

            if (primaryDown && now > nextFallbackPollAt)
            {
                nextFallbackPollAt = now + UnityEngine.Random.Range(FallbackPollMin, FallbackPollMax);
                StartCoroutine(FallbackPoll());
            }

            if (bootstrapDone && !DisableTelemetry && !killActive && !evicted && !shuttingDown && !primaryDown && !WsBusy && now > nextWsAttemptAt)
                ConnectWs();

            if (apiDomains.Count > 1 && primaryDown && now > nextApiRotateAt)
            {
                nextApiRotateAt = now + UnityEngine.Random.Range(FallbackPollMin, FallbackPollMax);
                int index = apiDomains.IndexOf(CurrentHost);
                if (index >= 0 && index < apiDomains.Count - 1)
                {
#if DEBUG
                    Dbg($"primary down, rotating to next api.json domain {apiDomains[index + 1]}");
#endif
                    SetEndpointHost(apiDomains[index + 1]);
                }
            }

            if (ws != null && ws.ReadyState == WebSocketState.Connecting && now > wsConnectingSince + 15f)
            {
#if DEBUG
                Dbg("ws connect timeout");
#endif
                try { ws.Close(); } catch { }
                ScheduleReconnect();
            }

            if (wsHelloed && now > nextHeartbeatAt)
            {
                nextHeartbeatAt = now + heartbeatInterval;
                TrySend(Frame(0x02, Array.Empty<byte>()));
#if DEBUG
                Dbg("heartbeat sent");
#endif
            }

            if (ws != null && ws.ReadyState == WebSocketState.Open && !wsHelloed && wsAckDeadline > 0f && now > wsAckDeadline)
            {
#if DEBUG
                Dbg("no HELLO_ACK, closing");
#endif
                try { ws.Close(1000, "no ack"); } catch { }
            }

            if (wsHelloed && wsExpectingConfig && now > wsExpectingConfigAt)
            {
                wsExpectingConfig = false;
#if DEBUG
                Dbg("no CONFIG push after HELLO_ACK, independent rev check");
#endif
                StartCoroutine(FetchConfig(true));
            }

            if (wsHelloed && now - lastServerContact > heartbeatInterval * 2f + 2f)
            {
#if DEBUG
                Dbg($"heartbeat starvation {now - lastServerContact:F1}s, dropping link");
#endif
                try { ws?.Close(1000, "starved"); } catch { }
            }

            if (wsHelloed && now > nextSafetyNetAt)
            {
                nextSafetyNetAt = now + CfgSafetyNet;
                safetyNetTicks++;
                StartCoroutine(FetchConfig(!AttestFresh()));

                if ((safetyNetTicks & 3) == 3)
                    StartCoroutine(FallbackPoll());
            }

            if (Pending.Count > 0 && now >= nextFlushTime)
            {
                nextFlushTime = now + FlushInterval;
                Flush();
            }
        }

        public static void Shutdown()
        {
            if (shuttingDown)
                return;

            shuttingDown = true;
#if DEBUG
            Dbg($"shutdown inRoom {inRoom} pending {Pending.Count}");
#endif
            if (inRoom)
            {
                Enqueue(Frame(0x11, new byte[] { 3 }));            // ROOM_EXIT reason=net
                inRoom = false;
                Sids.Clear();
            }

            Flush();

            senderDraining = true;
            try { senderThread?.Join(500); } catch { }

            try { ws?.Close(1001, "quit"); } catch { }
        }

        private void Start()
        {
            StartCoroutine(Bootstrap());
        }

        private static IEnumerator Bootstrap()
        {
            yield return FetchConfig();

            bootstrapDone = true;
            nextCfgPollAt = Time.unscaledTime + UnityEngine.Random.Range(FallbackPollMin, FallbackPollMax);

            if (primaryDown)
            {
                nextWsAttemptAt = Time.unscaledTime + UnityEngine.Random.Range(FallbackPollMin, FallbackPollMax);
                yield break;
            }

            if (killActive)
                yield break;

#if DEBUG
            if (Main.CurrentPrompt != null)
                Dbg("update prompt open, waiting before connecting");
#endif
            float deadline = Time.unscaledTime + 30f;
            while (Main.CurrentPrompt != null && Time.unscaledTime < deadline)
                yield return new WaitForSecondsRealtime(0.5f);

            ConnectWs();
        }

        private static IEnumerator FetchConfig(bool full = false)
        {
            string url = !full && heldRev > 0 ? $"{ConfigEndpoint}?rev={heldRev}" : $"{ConfigEndpoint}?rev=0";
            int generation = endpointGeneration;

            using UnityWebRequest request = UnityWebRequest.Get(url);
            request.timeout = 10;
            yield return request.SendWebRequest();

            bool reachable = request.result == UnityWebRequest.Result.Success && (request.responseCode == 200 || request.responseCode == 204);
#if DEBUG
            if (!reachable)
                Dbg($"cfg unreachable {request.responseCode} {request.error}");
#endif
            if (!reachable)
            {
                if (!WsOpen)
                    primaryDown = true;
                yield break;
            }

            primaryDown = false;

            if (request.responseCode == 204)
                yield break;

            string body = request.downloadHandler.text;
            Task.Run(() =>
            {
                ConfigResult verified = VerifyConfigJson(body);
                verified.Generation = generation;
                mainThread.Enqueue(() => ApplyVerifiedConfig(verified));
            });
        }

        public static bool TryVerify(string canonical, string signature)
        {
            lock (verifyLock)
                return TryVerifyCore(canonical, signature);
        }

        private static bool TryVerifyCore(string canonical, string signature)
        {
            if (string.IsNullOrEmpty(signature))
                return false;

            if (canonical == lastVerifyCanonical && signature == lastVerifySignature)
                return lastVerifyResult;

            bool ok = VerifySignature(canonical, signature);
            lastVerifyCanonical = canonical;
            lastVerifySignature = signature;
            lastVerifyResult = ok;
            return ok;
        }

        private static bool VerifySignature(string canonical, string signature)
        {
            byte[] sig;
            try { sig = Convert.FromBase64String(signature); }
            catch { return false; }

            if (verifyKey == null && !verifyBroken)
            {
                try
                {
                    byte[] spki = Convert.FromBase64String(SigningPublicKey);
                    verifyKey = new ManagedEcdsa();
                    verifyKey.ImportSubjectPublicKeyInfo(spki, out _);
#if DEBUG
                    using (SHA256 sha = SHA256.Create())
                        Dbg($"verify key fp {BitConverter.ToString(sha.ComputeHash(spki), 0, 4).Replace("-", "").ToLower()}");
#endif
                }
                catch (Exception e)
                {
                    verifyKey = null;
                    verifyBroken = true;
                    LogManager.LogError($"signature verify key rejected: {e.Message}");
                    return false;
                }
            }

            if (verifyKey == null)
                return false;

            try { return verifyKey.VerifyData(Encoding.ASCII.GetBytes(canonical), sig); }
            catch (Exception e)
            {
                LogManager.LogError($"signature verify failed: {e.Message}");
                return false;
            }
        }

        private static bool TryParseHourTs(string value, out DateTime parsed)
        {
            return DateTime.TryParseExact(value, "yyyy-MM-ddTHH\\Z", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed);
        }

        private static string CanonicalHost(string domain, string ts) => $"host\n{domain}\n{ts}";
        private static string CanonicalUpdate(string version, string sha256, string downloadUrl, string releaseUrl, string ts) =>
            $"update\n{version}\n{sha256}\n{downloadUrl}\n{releaseUrl}\n{ts}";
        private static string CanonicalApi(List<string> domains, string ts) => $"api\n{string.Join("\n", domains)}\n{ts}";

        private static bool AttestFresh()
        {
            if (!attestOk || attestDomain != CurrentHost)
                return false;

            double age = (DateTime.UtcNow - attestTs).TotalSeconds;
            return age <= AttestMaxAgeSec && age >= -ClockSkewSec;
        }

#if DEBUG
        private static string AttestFailReason()
        {
            if (!attestOk)
                return "not verified";
            if (attestDomain != CurrentHost)
                return $"domain {attestDomain} != {CurrentHost}";
            return $"age {(DateTime.UtcNow - attestTs).TotalSeconds:F0}s";
        }
#endif

        private sealed class ConfigResult
        {
            public string Error;
            public bool AttestOk;
            public string AttestDomain;
            public DateTime AttestTs;
            public int Rev;
            public bool Status;
            public string UpdateVersion;
            public string UpdateSha256;
            public string UpdateDownloadUrl;
            public string UpdateReleaseUrl;
            public bool UpdateVerified;
            public List<ModEntry> Mods;
            public List<AdminEntry> Admins;
            public int Generation;
        }

        private static ConfigResult VerifyConfigJson(string json)
        {
            ConfigResult result = new ConfigResult();
            result.Status = true;

            try
            {
                JObject data = JObject.Parse(json);

                result.AttestOk = false;
                result.AttestDomain = null;
                JObject attest = data["attest"] as JObject;
                if (attest != null)
                {
                    string domain = attest["domain"]?.Value<string>();
                    string ts = attest["timestamp"]?.Value<string>();
                    string signature = attest["signature"]?.Value<string>();

                    if (string.IsNullOrEmpty(domain) || string.IsNullOrEmpty(ts) || string.IsNullOrEmpty(signature))
                    {
#if DEBUG
                        Dbg($"attest rejected missing fields domain {domain != null} ts {ts != null} sig {signature != null}");
#endif
                    }
                    else if (!TryParseHourTs(ts, out DateTime parsed))
                    {
#if DEBUG
                        Dbg($"attest rejected bad ts {ts}");
#endif
                    }
                    else if (!TryVerify(CanonicalHost(domain, ts), signature))
                    {
#if DEBUG
                        Dbg($"attest rejected signature mismatch domain {domain} ts {ts}");
#endif
                    }
                    else
                    {
                        result.AttestOk = true;
                        result.AttestDomain = domain;
                        result.AttestTs = parsed;
#if DEBUG
                        double attestAge = (DateTime.UtcNow - result.AttestTs).TotalSeconds;
                        Dbg($"attest ok domain {domain} ts {ts} age {attestAge:F0}s");
#endif
                    }
                }

                result.Rev = data["rev"]?.Value<int>() ?? 0;
                result.Status = data["status"]?.Value<bool>() ?? true;

                JObject update = data["update"] as JObject;
                if (update != null)
                {
                    string version = update["version"]?.Value<string>();
                    string sha256 = update["sha256"]?.Value<string>();
                    string downloadUrl = update["downloadUrl"]?.Value<string>();
                    string releaseUrl = update["releaseUrl"]?.Value<string>();
                    string ts = update["timestamp"]?.Value<string>();

                    if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(ts))
                    {
#if DEBUG
                        Dbg("update envelope rejected missing fields");
#endif
                    }
                    else if (!TryVerify(CanonicalUpdate(version, sha256, downloadUrl, releaseUrl, ts), update["signature"]?.Value<string>()))
                    {
#if DEBUG
                        Dbg($"update envelope rejected signature mismatch version {version} ts {ts}");
#endif
                    }
                    else
                    {
                        result.UpdateVerified = true;
                        result.UpdateVersion = version;
                        result.UpdateSha256 = sha256;
                        result.UpdateDownloadUrl = downloadUrl;
                        result.UpdateReleaseUrl = releaseUrl;
#if DEBUG
                        Dbg($"update envelope verified version {version} ts {ts}");
#endif
                    }
                }

                result.Mods = VerifyMods(data["mods"] as JObject);
                result.Admins = VerifyAdmins(data["admins"] as JObject);
                return result;
            }
            catch (Exception e)
            {
                result.Error = e.Message;
                return result;
            }
        }

        private static void ApplyVerifiedConfig(ConfigResult result)
        {
            if (result.Generation != endpointGeneration)
                return;

            if (result.Error != null)
            {
                LogManager.LogError($"cfg parse failed: {result.Error}");
                return;
            }

            attestOk = result.AttestOk;
            attestDomain = result.AttestDomain;
            attestTs = result.AttestTs;

            if (wsHelloed && !AttestFresh())
            {
#if DEBUG
                Dbg("cfg body carries no valid attest for this host, dropping link");
#endif
                try { ws?.Close(1000, "attest"); } catch { }
            }

            if (result.Rev <= heldRev)
            {
#if DEBUG
                Dbg($"cfg ignored rev {result.Rev} <= held {heldRev}");
#endif
                return;
            }

            heldRev = result.Rev;

            ApplyMenuStatus(result.Status);

            if (result.Status)
                killActive = false;
            else
            {
                killActive = true;
                nextCfgPollAt = Time.unscaledTime + CfgPollKilled;
                suppressReconnectOnce = true;
                try { ws?.Close(1000, "killed"); } catch { }
            }

            if (result.UpdateVerified)
                ApplyVersionInfo(result.UpdateVersion, result.UpdateSha256, result.UpdateDownloadUrl, result.UpdateReleaseUrl);

            VerifiedMods.Clear();
            if (result.Mods != null)
                VerifiedMods.AddRange(result.Mods);

            VerifiedAdmins.Clear();
            if (result.Admins != null)
                VerifiedAdmins.AddRange(result.Admins);
        }

        public sealed class ModEntry
        {
            public string Name;
            public string File;
            public string Repo;
            public string Tag;
            public string Sha256;
        }

        public static readonly List<ModEntry> VerifiedMods = new List<ModEntry>();

        private static List<ModEntry> VerifyMods(JObject envelope)
        {
            List<ModEntry> parsed = new List<ModEntry>();

            if (envelope == null)
                return parsed;

            string ts = envelope["timestamp"]?.Value<string>();
            string signature = envelope["signature"]?.Value<string>();
            JArray entries = envelope["entries"] as JArray;

            if (string.IsNullOrEmpty(ts) || string.IsNullOrEmpty(signature) || entries == null || entries.Count == 0 || entries.Count > 8)
                return parsed;

            List<string> lines = new List<string>();

            foreach (JToken token in entries)
            {
                string name = token["name"]?.Value<string>();
                string file = token["file"]?.Value<string>();
                string repo = token["repo"]?.Value<string>();
                string tag = token["tag"]?.Value<string>();
                string sha256 = token["sha256"]?.Value<string>();

                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(file) || string.IsNullOrEmpty(repo) || string.IsNullOrEmpty(tag) || string.IsNullOrEmpty(sha256))
                {
                    parsed.Clear();
                    return parsed;
                }

                parsed.Add(new ModEntry { Name = name, File = file, Repo = repo, Tag = tag, Sha256 = sha256 });
                lines.Add($"{name}|{file}|{repo}|{tag}|{sha256}");
            }

            string canonical = "mods\n" + string.Join("\n", lines) + "\n" + ts;

            if (!TryVerify(canonical, signature))
            {
#if DEBUG
                Dbg("mods envelope rejected signature mismatch");
#endif
                parsed.Clear();
                return parsed;
            }

#if DEBUG
            Dbg($"mods envelope verified {parsed.Count} entries ts {ts}");
#endif
            return parsed;
        }

        public sealed class AdminEntry
        {
            public string Id;
            public string Role;
            public string Avatar;
        }

        public static readonly List<AdminEntry> VerifiedAdmins = new List<AdminEntry>();

        private static List<AdminEntry> VerifyAdmins(JObject envelope)
        {
            List<AdminEntry> parsed = new List<AdminEntry>();

            if (envelope == null)
                return parsed;

            string ts = envelope["timestamp"]?.Value<string>();
            string signature = envelope["signature"]?.Value<string>();
            JArray entries = envelope["entries"] as JArray;

            if (string.IsNullOrEmpty(ts) || string.IsNullOrEmpty(signature) || entries == null || entries.Count == 0 || entries.Count > 16)
                return parsed;

            List<string> lines = new List<string>();

            foreach (JToken token in entries)
            {
                string id = token["id"]?.Value<string>();
                string role = token["role"]?.Value<string>();
                string avatar = token["avatar"]?.Value<string>() ?? "";

                if (string.IsNullOrEmpty(id) || !System.Text.RegularExpressions.Regex.IsMatch(id, "^[0-9A-Fa-f]{8,40}$"))
                {
                    parsed.Clear();
                    return parsed;
                }
                if (string.IsNullOrEmpty(role) || role.Length > 24 || role.Contains('|') || role.Contains('\n'))
                {
                    parsed.Clear();
                    return parsed;
                }
                if (avatar.Length > 512 || avatar.Contains('|') || avatar.Contains('\n'))
                {
                    parsed.Clear();
                    return parsed;
                }
                if (avatar.Length > 0 && !avatar.StartsWith("https://") && !System.Text.RegularExpressions.Regex.IsMatch(avatar, "^[A-Za-z0-9_-]{1,32}$"))
                {
                    parsed.Clear();
                    return parsed;
                }

                parsed.Add(new AdminEntry { Id = id, Role = role, Avatar = avatar });
                lines.Add($"{id}|{role}|{avatar}");
            }

            string canonical = "admins\n" + string.Join("\n", lines) + "\n" + ts;

            if (!TryVerify(canonical, signature))
            {
#if DEBUG
                Dbg("admins envelope rejected signature mismatch");
#endif
                parsed.Clear();
                return parsed;
            }

#if DEBUG
            Dbg($"admins envelope verified {parsed.Count} entries ts {ts}");
#endif
            return parsed;
        }

        private static IEnumerator FallbackPoll()
        {
            using UnityWebRequest statusRequest = UnityWebRequest.Get(MetadataStatusUrl);
            statusRequest.timeout = 10;
            yield return statusRequest.SendWebRequest();

            if (statusRequest.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    JObject data = JObject.Parse(statusRequest.downloadHandler.text);
                    bool status = data["menustatus"]?.Value<bool>() ?? true;
#if DEBUG
                    Dbg($"fallback menustatus {status}");
#endif
                    if (!status)
                    {
                        if (!killActive)
                        {
                            ApplyMenuStatus(false);
                            killActive = true;
                            nextCfgPollAt = Time.unscaledTime + CfgPollKilled;
                            suppressReconnectOnce = true;
                            try { ws?.Close(1000, "killed"); } catch { }
                        }
                    }
                    else if (killActive && primaryDown)
                    {
                        killActive = false;
                        ApplyMenuStatus(true);
                    }
                }
                catch (Exception e)
                {
                    LogManager.LogError($"fallback status parse failed: {e.Message}");
                }
            }

            using UnityWebRequest versionRequest = UnityWebRequest.Get(MetadataVersionUrl);
            versionRequest.timeout = 10;
            yield return versionRequest.SendWebRequest();

            if (versionRequest.result != UnityWebRequest.Result.Success)
                yield break;

            try
            {
                JObject update = JObject.Parse(versionRequest.downloadHandler.text);
                string version = update["version"]?.Value<string>();
                string ts = update["timestamp"]?.Value<string>();

                if (version == null || ts == null)
                {
#if DEBUG
                    Dbg("fallback update envelope rejected missing fields");
#endif
                }
                else if (!TryVerify(CanonicalUpdate(version, update["sha256"]?.Value<string>(), update["downloadUrl"]?.Value<string>(), update["releaseUrl"]?.Value<string>(), ts), update["signature"]?.Value<string>()))
                {
#if DEBUG
                    Dbg($"fallback update envelope rejected signature mismatch version {version} ts {ts}");
#endif
                }
                else
                {
                    ApplyVersionInfo(
                        version,
                        update["sha256"]?.Value<string>(),
                        update["downloadUrl"]?.Value<string>(),
                        update["releaseUrl"]?.Value<string>());
#if DEBUG
                    Dbg($"fallback update verified version {version} ts {ts}");
#endif
                }
            }
            catch (Exception e)
            {
                LogManager.LogError($"fallback version parse failed: {e.Message}");
            }

            using UnityWebRequest apiRequest = UnityWebRequest.Get(MetadataApiUrl);
            apiRequest.timeout = 10;
            yield return apiRequest.SendWebRequest();

            if (apiRequest.result != UnityWebRequest.Result.Success)
                yield break;

            try
            {
                JObject api = JObject.Parse(apiRequest.downloadHandler.text);
                JArray domainsArray = api["domains"] as JArray;
                string apiTs = api["timestamp"]?.Value<string>();

                if (domainsArray != null && apiTs != null && TryParseHourTs(apiTs, out DateTime parsed) && parsed > heldApiTs)
                {
                    List<string> domains = domainsArray
                        .Select(d => d?.Value<string>())
                        .Where(d => !string.IsNullOrEmpty(d) && Regex.IsMatch(d, "^[A-Za-z0-9.-]{1,253}$"))
                        .Take(8)
                        .ToList();

                    if (domains.Count == 0)
                    {
#if DEBUG
                        Dbg("api.json rejected no valid domains");
#endif
                    }
                    else if (!TryVerify(CanonicalApi(domains, apiTs), api["signature"]?.Value<string>()))
                    {
#if DEBUG
                        Dbg("api.json rejected signature mismatch, keeping endpoints");
#endif
                    }
                    else
                    {
                        heldApiTs = parsed;
                        apiDomains = domains;
                        SetEndpointHost(domains[0]);
                        nextApiRotateAt = 0f;
#if DEBUG
                        Dbg($"api.json adopted host {domains[0]} ts {apiTs}");
#endif
                    }
                }
                else
                {
#if DEBUG
                    Dbg($"api.json ignored domains {(domainsArray != null ? "y" : "n")} ts {apiTs ?? "none"}");
#endif
                }
            }
            catch (Exception e)
            {
                LogManager.LogError($"api.json parse failed: {e.Message}");
            }
        }

        private static void SetEndpointHost(string host)
        {
            if (host == CurrentHost)
                return;

            CurrentHost = host;
            WireEndpoint = "wss://" + host + "/v2/ws";
            ConfigEndpoint = "https://" + host + "/v2/cfg";
            heldRev = 0;
            attestOk = false;
            attestDomain = null;
            endpointGeneration++;
            suppressReconnectOnce = true;
            try { ws?.Close(1000, "hostchange"); } catch { }
        }

        private static void ConnectWs()
        {
            if (shuttingDown || evicted || DisableTelemetry)
                return;

            if (WsBusy)
                return;

            if (!AttestFresh())
            {
                attestRetryDelay = attestRetryDelay <= 0f ? 5f : Mathf.Min(attestRetryDelay * 2f, 60f);
                nextWsAttemptAt = Time.unscaledTime + attestRetryDelay;
#if DEBUG
                Dbg($"no fresh attest {AttestFailReason()}, refetching cfg (retry in {attestRetryDelay:F0}s)");
#endif
                instance.StartCoroutine(FetchConfig(true));
                return;
            }

            attestRetryDelay = 0f;

            while (sendQueue.TryDequeue(out _)) { }

            WebSocket socket = new WebSocket(WireEndpoint);
            ws = socket;
            wsConnectingSince = Time.unscaledTime;
            wsAckDeadline = 0f;
            wsHelloed = false;
            wsExpectingConfig = false;
            unknownFrameChecked = false;

            socket.OnOpen += (sender, args) => mainThread.Enqueue(() =>
            {
                connectFailures = 0;
                reconnectDelay = 0;
                primaryDown = false;
                wsAckDeadline = Time.unscaledTime + 10f;
                byte[] hello = BuildHello();
                TrySend(hello);
#if DEBUG
                Dbg($"ws open, HELLO {hello.Length}B fromMachine {InstallIdFromMachine} version {PluginInfo.Version}");
#endif
            });

            socket.OnMessage += (sender, args) => mainThread.Enqueue(() => HandleServerData(args.RawData));

            socket.OnClose += (sender, args) => mainThread.Enqueue(() => HandleWsClosed(args.Code));

            socket.OnError += (sender, args) => mainThread.Enqueue(() =>
            {
#if DEBUG
                Dbg($"ws error {args.Message}");
#endif
                if (ws == socket && socket.ReadyState == WebSocketState.Closed)
                    ScheduleReconnect();
            });

            try
            {
                socket.ConnectAsync();
#if DEBUG
                Dbg($"ws connecting {WireEndpoint}");
#endif
            }
            catch (Exception e)
            {
                LogManager.LogError($"ws connect failed: {e.Message}");
                connectFailures++;
                ScheduleReconnect();
            }
        }

        private static bool WsOpen => ws != null && ws.ReadyState == WebSocketState.Open;

        private static bool WsBusy => ws != null && (ws.ReadyState == WebSocketState.Connecting || ws.ReadyState == WebSocketState.Open);

        private static bool TrySend(byte[] frame)
        {
            if (!WsOpen)
                return false;

            sendQueue.Enqueue(frame);
            return true;
        }

        private static void SenderLoop()
        {
            while (true)
            {
                if (!sendQueue.TryDequeue(out byte[] frame))
                {
                    if (senderDraining)
                        return;
                    Thread.Sleep(25);
                    continue;
                }

                try
                {
                    WebSocket socket = ws;
                    if (socket != null && socket.ReadyState == WebSocketState.Open)
                        socket.Send(frame);
                }
                catch { }
            }
        }

        private static void HandleWsClosed(ushort code)
        {
            bool wasHelloed = wsHelloed;
            wsHelloed = false;
            wsExpectingConfig = false;
#if DEBUG
            Dbg($"ws closed {code} wasHelloed {wasHelloed}");
#endif
            if (shuttingDown)
                return;

            if (suppressReconnectOnce)
            {
                suppressReconnectOnce = false;
                return;
            }

            if (!wasHelloed)
                connectFailures++;

            ScheduleReconnect();

            if (!killActive && wasHelloed)
                instance.StartCoroutine(FetchConfig());
        }

        private static void ScheduleReconnect()
        {
            float delay;

            if (connectFailures <= 2)
            {
                reconnectDelay = reconnectDelay <= 0f ? 1f : reconnectDelay * 2f;
                reconnectDelay = Mathf.Min(reconnectDelay, 60f);
                delay = reconnectDelay;
            }
            else
            {
                delay = UnityEngine.Random.Range(FallbackPollMin, FallbackPollMax);
                reconnectDelay = 60f;
            }

            nextWsAttemptAt = Time.unscaledTime + delay * UnityEngine.Random.Range(0.7f, 1.3f);
#if DEBUG
            Dbg($"reconnect in {delay:F0}s (failures {connectFailures})");
#endif
        }

        private static void HandleServerData(byte[] data)
        {
            int offset = 0;
            while (offset < data.Length)
            {
                byte type = data[offset++];
                if (!TryReadVarint(data, ref offset, out int length) || offset + length > data.Length)
                {
#if DEBUG
                    Dbg($"server frame malformed at offset {offset}");
#endif
                    return;
                }

                byte[] payload = new byte[length];
                Buffer.BlockCopy(data, offset, payload, 0, length);
                offset += length;
                HandleServerFrame(type, payload);
            }
        }

        private static void HandleServerFrame(byte type, byte[] payload)
        {
            switch (type)
            {
                case 0x81:
                    if (payload.Length >= 14)
                    {
                        protocolKicks = 0;
                        connectFailures = 0;
                        wsHelloed = true;
                        lastServerContact = Time.unscaledTime;
                        heartbeatInterval = Mathf.Clamp(payload[12] | (payload[13] << 8), 5, 60);
                        nextHeartbeatAt = Time.unscaledTime + heartbeatInterval;
                        nextSafetyNetAt = Time.unscaledTime + CfgSafetyNet;
                        wsExpectingConfig = true;
                        wsExpectingConfigAt = Time.unscaledTime + 2f;
#if DEBUG
                        int session = payload[0] | (payload[1] << 8) | (payload[2] << 16) | (payload[3] << 24);
                        Dbg($"HELLO_ACK session {session} heartbeat {heartbeatInterval}s");
#endif
                        if (inRoom && PhotonNetwork.InRoom)
                        {
                            currentRoom = PhotonNetwork.CurrentRoom.Name;
                            currentRegion = PhotonNetwork.CloudRegion;
                            currentRoomVisible = PhotonNetwork.CurrentRoom.IsVisible;

                            Enqueue(Frame(0x10, EncodeRoomEnter()));
                            instance.StartCoroutine(RoomStateAfterRigsSettle());
                        }
                    }
                    break;

                case 0x82:
                    lastServerContact = Time.unscaledTime;
                    break;

                case 0x83:
                {
                    wsExpectingConfig = false;
                    int offset = 0;
                    if (TryReadVarint(payload, ref offset, out int length) && offset + length <= payload.Length)
                    {
                        string body = Encoding.ASCII.GetString(payload, offset, length);
                        int generation = endpointGeneration;
                        Task.Run(() =>
                        {
                            ConfigResult verified = VerifyConfigJson(body);
                            verified.Generation = generation;
                            mainThread.Enqueue(() => ApplyVerifiedConfig(verified));
                        });
                    }
                    break;
                }

                case 0x84:
                    if (payload.Length >= 2)
                        HandleKick(payload[0], payload[1]);
                    break;

                default:
#if DEBUG
                    Dbg($"unknown server frame 0x{type:X2} len {payload.Length}");
#endif
                    if (!unknownFrameChecked)
                    {
                        unknownFrameChecked = true;
                        instance.StartCoroutine(FetchConfig());
                    }
                    break;
            }
        }

        private static void HandleKick(byte reason, byte backoff)
        {
#if DEBUG
            Dbg($"KICK reason {reason} backoff {backoff}s");
#endif
            suppressReconnectOnce = true;

            switch (reason)
            {
                case 0:
                    ApplyMenuStatus(false);
                    killActive = true;
                    nextCfgPollAt = Time.unscaledTime + CfgPollKilled;
                    break;

                case 1:
                    evicted = true;
                    LogManager.Log("telemetry session evicted, standing down until restart");
                    break;

                case 2:
                    nextWsAttemptAt = Time.unscaledTime + Mathf.Max(1f, backoff);
                    break;

                case 3:
                    protocolKicks++;
                    if (protocolKicks >= 3)
                    {
                        evicted = true;
                        LogManager.LogError("telemetry rejected our frames 3 times, we are broken, giving up");
                    }
                    else
                        nextWsAttemptAt = Time.unscaledTime + Mathf.Max(1f, backoff);
                    break;

                case 4:
                    nextWsAttemptAt = Time.unscaledTime + Mathf.Max(30f, backoff);
                    break;
            }
        }

        private static byte[] BuildHello()
        {
            Writer w = new Writer();
            w.Write(3);
            w.AppendBytes(GetInstallId());
            w.Write((byte)((InstallIdFromMachine ? 2 : 0) | (PluginInfo.BetaBuild ? 4 : 0)));
            WriteString(w, Cap(PluginInfo.Version, 32));
            WriteString(w, SelfUid());
            return Frame(0x01, w.Bytes);
        }

        private static bool TryReadVarint(byte[] buffer, ref int offset, out int value)
        {
            long result = 0;
            int shift = 0;

            while (offset < buffer.Length)
            {
                byte b = buffer[offset++];
                result += (b & 0x7f) * (1L << shift);
                if ((b & 0x80) == 0)
                {
                    if (result > int.MaxValue)
                    {
                        value = 0;
                        return false;
                    }
                    value = (int)result;
                    return true;
                }
                shift += 7;
                if (shift > 35)
                    break;
            }

            value = 0;
            return false;
        }

        private static void OnJoinRoom()
        {
            inRoom = true;
            currentRoom = PhotonNetwork.CurrentRoom.Name;
            currentRegion = PhotonNetwork.CloudRegion;
            currentRoomVisible = PhotonNetwork.CurrentRoom.IsVisible;

#if DEBUG
            Dbg($"join room {currentRoom} region {currentRegion} visible {currentRoomVisible} players {PhotonNetwork.PlayerList.Length}");
#endif
            Sids.Clear();
            nextSid = 0;

            Enqueue(Frame(0x10, EncodeRoomEnter()));               // ROOM_ENTER

            instance.StartCoroutine(RoomStateAfterRigsSettle());   // ROOM_STATE
        }

        private static void OnLeaveRoom()
        {
            if (!inRoom)
            {
#if DEBUG
                Dbg("leave event but not in room, ignoring");
#endif
                return;
            }

            inRoom = false;
            Sids.Clear();

#if DEBUG
            Dbg($"leave room {currentRoom} reason manual");
#endif
            Enqueue(Frame(0x11, new byte[] { 0 }));                 // ROOM_EXIT reason=manual
        }

        private static void OnPlayerJoin(NetPlayer player)
        {
            if (!inRoom || player == null)
            {
#if DEBUG
                Dbg($"player join ignored, inRoom {inRoom} player null {player == null}");
#endif
                return;
            }

            ushort sid = AssignSid(player);
#if DEBUG
            Dbg($"player join {player.NickName} sid {sid}, waiting for rig");
#endif
            instance.StartCoroutine(PlayerJoinWhenRigResolves(player, sid));
        }

        private static void OnPlayerLeave(NetPlayer player)
        {
            if (!inRoom || player == null || !Sids.TryGetValue(player, out ushort sid))
            {
#if DEBUG
                Dbg($"player leave ignored, inRoom {inRoom} player null {player == null}");
#endif
                return;
            }

            Sids.Remove(player);
#if DEBUG
            Dbg($"player leave {player.NickName} sid {sid}");
#endif
            Enqueue(Frame(0x13, EncodeU16(sid)));                   // PLAYER_LEAVE
        }

        private static IEnumerator RoomStateAfterRigsSettle()
        {
            yield return new WaitForSecondsRealtime(RoomStateDelay);

            if (!inRoom || PhotonNetwork.CurrentRoom == null)
            {
#if DEBUG
                Dbg($"room state cancelled, inRoom {inRoom} currentRoom null {PhotonNetwork.CurrentRoom == null}");
#endif
                yield break;
            }

            currentRoom = PhotonNetwork.CurrentRoom.Name;
            currentRegion = PhotonNetwork.CloudRegion;
            currentRoomVisible = PhotonNetwork.CurrentRoom.IsVisible;

#if DEBUG
            Dbg($"room state snapshot room {currentRoom} region {currentRegion} visible {currentRoomVisible} players {PhotonNetwork.PlayerList.Length}");
#endif
            Enqueue(Frame(0x14, EncodeRoomState()));
        }

        private static IEnumerator PlayerJoinWhenRigResolves(NetPlayer player, ushort sid)
        {
            VRRig rig = null;
            float started = Time.unscaledTime;
            float deadline = Time.unscaledTime + RigWaitTimeout;

            while (Time.unscaledTime < deadline)
            {
                rig = RigUtilities.GetVRRigFromPlayer(player);
                if (rig != null)
                    break;

                yield return new WaitForSecondsRealtime(0.25f);
            }

            if (rig != null && (rig._playerOwnedCosmetics == null || rig._playerOwnedCosmetics.Count == 0))
            {
                float cosmeticDeadline = Time.unscaledTime + CosmeticWaitTimeout;
                while (Time.unscaledTime < cosmeticDeadline)
                {
                    if (rig._playerOwnedCosmetics != null && rig._playerOwnedCosmetics.Count > 0)
                        break;

                    yield return new WaitForSecondsRealtime(0.25f);
                }
            }

#if DEBUG
            Dbg($"rig resolve for {player.NickName} sid {sid} found {rig != null} after {Time.unscaledTime - started:F2}s");
#endif
            if (!inRoom || !Sids.TryGetValue(player, out ushort assigned) || assigned != sid)
                yield break;

            Enqueue(Frame(0x12, EncodePlayerEntry(sid, player.NickName, rig, player.UserId)));
        }

        private static ushort AssignSid(NetPlayer player)
        {
            if (Sids.TryGetValue(player, out ushort existing))
            {
#if DEBUG
                Dbg($"sid reuse {existing} for {player.NickName}");
#endif
                return existing;
            }

            Sids[player] = nextSid;
#if DEBUG
            Dbg($"sid assign {nextSid} to {player.NickName}");
#endif
            return nextSid++;
        }

        public static IEnumerator ReportFailureMessage(string error)
        {
            if (!Enabled || DisableTelemetry)
            {
#if DEBUG
                Dbg($"reportban gated, enabled {Enabled} disableTelemetry {DisableTelemetry}");
#endif
                yield break;
            }

#if DEBUG
            Dbg($"reportban error text {error}");
#endif
            AchievementManager.UnlockAchievement(new AchievementManager.Achievement
            {
                name = "Purgatory",
                description = "Get banned with the menu.",
                icon = "Images/Achievements/banned.png"
            });

            List<string> enabledMods = new List<string>();

            int categoryIndex = 0;
            foreach (ButtonInfo[] category in Buttons.buttons)
            {
                if (!Buttons.categoryNames[categoryIndex].Contains("Settings"))
                    enabledMods.AddRange(category.Where(button => button.enabled).Select(button => CleanModName(button.overlapText ?? button.buttonText)));

                categoryIndex++;
            }

#if DEBUG
            Dbg($"reportban mods {enabledMods.Count} [{string.Join(", ", enabledMods.Take(50))}]");
#endif
            Enqueue(Frame(0x20, EncodeReportBan(PhotonNetwork.NickName, PluginInfo.Version, error, enabledMods.Take(50).ToList())));
        }

        private static string CleanModName(string name)
        {
            name = Regex.Replace(name, "<.*?>", string.Empty);
            if (name.Length > 128)
                name = name[..127];
            return name.ToUpperInvariant();
        }

        public static void ApplyMenuStatus(bool enabled)
        {
            bool wasDisabled = Main.MenuDisabled;
            Main.MenuDisabled = !enabled;

#if DEBUG
            Dbg($"menu status enabled {enabled} wasDisabled {wasDisabled} menuDisabled now {Main.MenuDisabled}");
#endif
            if (enabled)
                return;

            try
            {
                if (Main.menu != null)
                    Main.CloseMenu();
            }
            catch { }

            if (!wasDisabled)
                NotificationManager.SendNotification("<color=red>[</color><color=white>ALERT</color><color=red>]</color> <color=white>The menu has been disabled by the developers. Check </color><color=red>discord.gg/iidk</color><color=white> for updates.</color>", 15000);
        }

        public static void ApplyVersionInfo(string version, string publishedHash, string downloadUrl, string releaseUrl)
        {
            bool hasVersion = !string.IsNullOrEmpty(version);
            bool hashOk = publishedHash != null && Regex.IsMatch(publishedHash, "^[0-9a-fA-F]{64}$");
            bool urlOk = !string.IsNullOrEmpty(downloadUrl);

            if (hasVersion && (!hashOk || !urlOk))
            {
#if DEBUG
                Dbg($"update envelope incomplete (hash {publishedHash?.Length ?? 0} chars, url {(urlOk ? "present" : "missing")}), ignoring");
#endif
                return;
            }
            string candidate = (downloadUrl ?? string.Empty).Trim();
            UpdateDownloadUrl = (candidate.StartsWith("https://", StringComparison.Ordinal)
                && !candidate.Contains("..")
                && candidate.IndexOfAny(new[] { '"', '\'', '$', '`', '&', '|', ';', '\\', ' ', '\t', '\r', '\n', '<', '>', '^', '%' }) < 0
                && candidate.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) ? candidate : null;
            UpdateReleaseUrl = releaseUrl;
            UpdateSha256 = publishedHash ?? "";
            LatestVersion = version;

#if DEBUG
            Dbg($"version info version {version} hash {publishedHash} downloadUrl {downloadUrl} releaseUrl {releaseUrl}");
            Dbg($"downloadUrl validation {(UpdateDownloadUrl == null ? "rejected" : "accepted")}");
#endif
            if (string.IsNullOrEmpty(version))
                return;

            if (string.Equals(version, PluginInfo.Version, StringComparison.OrdinalIgnoreCase))
            {
#if DEBUG
                Dbg("version equals local, running integrity check");
#endif
                CheckLocalBuildIntegrity(publishedHash);
                return;
            }

            if (!IsBehindSameScheme(PluginInfo.Version, version) || OutdatedVersion)
            {
#if DEBUG
                Dbg($"update skipped, alreadyOutdated {OutdatedVersion}");
#endif
                return;
            }

            OutdatedVersion = true;

            LogManager.Log($"A new version of the menu is available ({version}, running {PluginInfo.Version})");
            NotificationManager.SendNotification($"<color=grey>[</color><color=red>OUTDATED</color><color=grey>]</color> A new version of the menu is available (v{version}). Please download it here: {UpdateReleaseUrl}", 10000);
            Main.UpdatePrompt(version);
        }

        private static void CheckLocalBuildIntegrity(string publishedHash)
        {
            if (string.IsNullOrEmpty(publishedHash))
            {
#if DEBUG
                Dbg("integrity check skipped, no published hash");
#endif
                return;
            }

            Task.Run(() =>
            {
                string localHash = null;
                string error = null;

                try
                {
                    string location = Assembly.GetExecutingAssembly().Location;
                    if (string.IsNullOrEmpty(location) || !File.Exists(location))
                    {
#if DEBUG
                        Dbg($"integrity check skipped, assembly location missing {location}");
#endif
                    }
                    else
                    {
                        using FileStream stream = File.OpenRead(location);
                        using SHA256 sha256 = SHA256.Create();
                        byte[] hash = sha256.ComputeHash(stream);
                        localHash = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
                    }
                }
                catch (Exception e)
                {
                    error = e.Message;
                }

                mainThread.Enqueue(() =>
                {
                    if (error != null)
                    {
                        LogManager.LogError($"Build integrity check failed: {error}");
                        return;
                    }

                    if (localHash == null)
                        return;

                    ApplyBuildIntegrity(localHash, publishedHash);
                });
            });
        }

        private static void ApplyBuildIntegrity(string localHash, string publishedHash)
        {
            bool matchesRelease = string.Equals(localHash, publishedHash, StringComparison.OrdinalIgnoreCase);

#if DEBUG
            Dbg($"integrity local {localHash} published {publishedHash} match {matchesRelease}");
#endif
            PluginInfo.BetaBuild = !matchesRelease;

            if (!matchesRelease && !betaBuildWarned)
            {
                betaBuildWarned = true;
                LogManager.Log("Running a modified build of the menu (DLL hash does not match the release)");
                NotificationManager.SendNotification("<color=grey>[</color><color=blue>DEV BUILD</color><color=grey>]</color> This DLL does not match the published release, so it counts as a development build. Bugs are expected.", 10000);
            }
        }

        private static bool IsBehindSameScheme(string ours, string theirs)
        {
            if (!Version.TryParse(ours ?? string.Empty, out Version ourVersion) ||
                !Version.TryParse(theirs ?? string.Empty, out Version theirVersion))
                return false;

            bool behind = ourVersion.Major == theirVersion.Major && ourVersion < theirVersion;
#if DEBUG
            Dbg($"behindSameScheme ours {ours} theirs {theirs} result {behind}");
#endif
            return behind;
        }

        private static void Enqueue(byte[] frame)
        {
            if (!Enabled || DisableTelemetry || Main.MenuDisabled)
                return;

            if (frame.Length > FrameMax)
            {
#if DEBUG
                Dbg($"frame 0x{frame[0]:X2} over {FrameMax} ({frame.Length}B), dropping");
#endif
                return;
            }

            while (Pending.Count >= MaxQueued)
            {
#if DEBUG
                Dbg($"queue full {MaxQueued}, dropping oldest");
#endif
                Pending.Dequeue();
            }

            Pending.Enqueue(frame);
#if DEBUG
            Dbg($"enqueue type 0x{frame[0]:X2} len {frame.Length} pending {Pending.Count} hex {Hex(frame)}");
#endif
        }

        private static void Flush()
        {
            if (!wsHelloed || !WsOpen)
                return;

            int budget = shuttingDown ? int.MaxValue : 32;
            int sent = 0;

            while (Pending.Count > 0 && sent < budget)
            {
                byte[] frame = Pending.Peek();
                if (!TrySend(frame))
                    break;

                Pending.Dequeue();
                sent++;
            }
#if DEBUG
            if (sent > 0)
                Dbg($"flush sent {sent}, pending {Pending.Count}");
#endif
        }

        // Frame = [u8 type][varint payload_len][payload].

        private static byte[] Frame(byte type, byte[] payload)
        {
            Writer w = new Writer();
            w.Write(type);
            w.WriteVarint(payload.Length);
            w.AppendBytes(payload);
            return w.Bytes;
        }

        private static string Cap(string value, int max) =>
            value != null && value.Length > max ? value[..max] : value;

        private static string CleanUid(string value)
        {
            if (value == null || !Regex.IsMatch(value, "^[0-9A-Fa-f]{8,40}$"))
                return string.Empty;
            return value.ToUpperInvariant();
        }

        private static string SelfUid()
        {
            if (selfUid == null)
            {
                try { selfUid = CleanUid(PlayFabAuthenticator.instance.GetPlayFabPlayerId()); }
                catch { selfUid = string.Empty; }
            }
            return selfUid;
        }

        // 0x10 ROOM_ENTER: RoomRef, region, flags(bit0 = room visible), game mode, player count, server address(<=64, optional trailing)
        private static byte[] EncodeRoomEnter()
        {
            Writer w = new Writer();
            WriteRoomRef(w, currentRoom);
            WriteString(w, Cap(currentRegion, 16));
            w.Write((byte)(currentRoomVisible ? 1 : 0));
            WriteString(w, Cap(NetworkSystem.Instance.GameModeString, 128));
            w.WriteVarint(PhotonNetwork.PlayerList.Length);
            WriteString(w, Cap(PhotonNetwork.ServerAddress, 64));
            return w.Bytes;
        }

        // 0x12 PLAYER_JOIN / ROOM_STATE entry: sid BE, nickname(<=12), platform(0 PC 1 Quest 2 unknown; PC via "FIRST LOGIN" cosmetics marker or >=2 custom properties), rgb, cosmetics<=10
        private static byte[] EncodePlayerEntry(ushort sid, string nickname, VRRig rig, string uid)
        {
            Writer w = new Writer();
            w.Write((byte)(sid >> 8));
            w.Write((byte)(sid & 0xFF));

            if (nickname != null && nickname.Length > 12)
                nickname = nickname[..12];

            WriteString(w, nickname);

            byte platform = 2;
            Color color = Color.black;

            if (rig != null)
            {
                platform = (byte)(IsSteamPlayer(rig) ? 0 : 1);
                color = rig.playerColor;
            }

            w.Write(platform);
            w.Write((byte)Math.Round(color.r * 255));
            w.Write((byte)Math.Round(color.g * 255));
            w.Write((byte)Math.Round(color.b * 255));

            if (rig?._playerOwnedCosmetics == null)
            {
                w.WriteVarint(0);
            }
            else
            {
                string[] cosmetics = rig._playerOwnedCosmetics.Take(10).ToArray();
                w.WriteVarint(cosmetics.Length);
                foreach (string cosmetic in cosmetics)
                    WriteString(w, Cap(cosmetic, 32));
            }

            WriteString(w, CleanUid(uid));

#if DEBUG
            Dbg($"entry sid {sid} nick {nickname} platform {platform} rgb {(int)Math.Round(color.r * 255)} {(int)Math.Round(color.g * 255)} {(int)Math.Round(color.b * 255)} cosmetics {(rig?._playerOwnedCosmetics == null ? 0 : rig._playerOwnedCosmetics.Count())}");
#endif
            return w.Bytes;
        }

        // 0x14 ROOM_STATE: RoomRef, region str, flags, count<=32, entries
        private static byte[] EncodeRoomState()
        {
            Writer w = new Writer();
            WriteRoomRef(w, currentRoom);
            WriteString(w, currentRegion);
            w.Write((byte)(currentRoomVisible ? 1 : 0));

            var players = PhotonNetwork.PlayerList;
            int count = Math.Min(players.Length, 32);
            w.WriteVarint(count);
            for (int i = 0; i < count; i++)
            {
                Player identification = players[i];
                w.AppendBytes(EncodePlayerEntry(AssignSid(identification), identification.NickName, RigUtilities.GetVRRigFromPlayer(identification), identification.UserId));
            }
#if DEBUG
            Dbg($"room state encoded players {count} bytes {w.Bytes.Length}");
#endif
            return w.Bytes;
        }

        // 0x20 REPORT_BAN: username raw <=32, version, failure text <=512, enabled mods <=50
        private static byte[] EncodeReportBan(string username, string version, string error, List<string> enabledMods)
        {
            Writer w = new Writer();

            WriteString(w, Cap(username, 32));
            WriteString(w, Cap(version, 32));
            WriteString(w, Cap(error, 512));
            w.WriteVarint(enabledMods.Count);
            foreach (string mod in enabledMods)
                WriteString(w, mod);

            WriteString(w, SelfUid());
#if DEBUG
            Dbg($"reportban encoded user {username} version {version} mods {enabledMods.Count} bytes {w.Bytes.Length}");
#endif
            return w.Bytes;
        }

        private static bool IsSteamPlayer(VRRig rig)
        {
            string cosmetics = rig.CosmeticsString();
#if DEBUG
            Dbg($"steam check cosmetics [{cosmetics}] props {rig.Creator.GetPlayerRef().CustomProperties.Count}");
#endif
            return cosmetics.Contains("S. FIRST LOGIN") || cosmetics.Contains("FIRST LOGIN") || rig.Creator.GetPlayerRef().CustomProperties.Count >= 2;
        }

        // RoomRef: [u8 form][0: u32 LE base-40 of 6 chars][1: varint-len ASCII]
        // 6 chars of [A-Z0-9_.-~] pack into u32 base-40 (40^6 < 2^32).
        private const string RoomAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_.-~";

        private static void WriteRoomRef(Writer w, string room)
        {
            room = (room ?? string.Empty).ToUpperInvariant();

            if (room.Length == 6 && room.All(c => RoomAlphabet.IndexOf(c) >= 0))
            {
                uint value = 0;
                foreach (char c in room)
                    value = value * 40 + (uint)RoomAlphabet.IndexOf(c);

#if DEBUG
                Dbg($"roomref packed {room} u32 {value}");
#endif
                w.Write(0);
                w.Write((byte)(value & 0xFF));
                w.Write((byte)((value >> 8) & 0xFF));
                w.Write((byte)((value >> 16) & 0xFF));
                w.Write((byte)((value >> 24) & 0xFF));
            }
            else
            {
#if DEBUG
                Dbg($"roomref raw fallback {room}");
#endif
                w.Write(1);
                WriteString(w, room);
            }
        }

        private static void WriteString(Writer w, string value)
        {
            value ??= string.Empty;
            w.WriteVarint(value.Length);
            foreach (char c in value)
                w.Write((byte)c);
        }

        private static byte[] EncodeU16(ushort value) => new byte[] { (byte)(value >> 8), (byte)(value & 0xFF) };

        private sealed class Writer
        {
            private readonly List<byte> buffer = new List<byte>(32);

            public void Write(byte b) => buffer.Add(b);
            public void AppendBytes(byte[] bytes) => buffer.AddRange(bytes);
            public void WriteVarint(int value) => WriteVarint(buffer, value);

            private static void WriteVarint(List<byte> buffer, int value)
            {
                uint v = (uint)value;
                while (v >= 0x80)
                {
                    buffer.Add((byte)(v | 0x80));
                    v >>= 7;
                }
                buffer.Add((byte)v);
            }

            public byte[] Bytes => buffer.ToArray();
        }
    }
}
