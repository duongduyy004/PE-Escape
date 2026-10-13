using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using KMA.Gameplay;

namespace KMA.EditorTools
{
    public static class StudentJourneyContentBuilder
    {
        const string ChallengeFolder = "Assets/_Project/ScriptableObjects/Journey";
        const string ResourcesFolder = "Assets/_Project/Resources/Journey";
        const string CatalogPath = ResourcesFolder + "/ChallengeCatalog.asset";
        const string SprintBalancePath = ResourcesFolder + "/SprintBalance.asset";
        const string DialoguePath = ResourcesFolder + "/JourneyDialogues.asset";

        readonly struct ChallengeSpec
        {
            public readonly string Id;
            public readonly SubjectId Subject;
            public readonly ChallengeKind Kind;
            public readonly float Distance;
            public readonly float TimeLimit;
            public readonly int TargetCount;
            public readonly bool KeeperEnabled;
            public readonly ChallengeDifficulty Difficulty;
            public readonly bool TimingHelp;
            public readonly string Objective;
            public readonly int AttemptLimit;

            public ChallengeSpec(string id, SubjectId subject, ChallengeKind kind, float distance,
                float timeLimit, int targetCount, bool keeperEnabled, ChallengeDifficulty difficulty,
                bool timingHelp, string objective, int attemptLimit = 0)
            {
                Id = id;
                Subject = subject;
                Kind = kind;
                Distance = distance;
                TimeLimit = timeLimit;
                TargetCount = targetCount;
                KeeperEnabled = keeperEnabled;
                Difficulty = difficulty;
                TimingHelp = timingHelp;
                Objective = objective;
                AttemptLimit = attemptLimit;
            }
        }

        [MenuItem("KMA/Journey/Build Challenge Assets")]
        public static void BuildChallenges()
        {
            EnsureFolder(ChallengeFolder);
            EnsureFolder(ResourcesFolder);

            ChallengeSpec[] specs =
            {
                new ChallengeSpec("sprint_learn", SubjectId.Sprint, ChallengeKind.Learn,
                    0f, 50f, 12, false, ChallengeDifficulty.Normal, true,
                    "Tập bấm TRÁI, PHẢI luân phiên trong 50 giây. Cần đạt chuỗi 12 nhịp hợp lệ ít nhất một lần."),
                new ChallengeSpec("sprint_practice", SubjectId.Sprint, ChallengeKind.Practice,
                    150f, 45f, 0, false, ChallengeDifficulty.Normal, true,
                    "Chạy 150 m trong tối đa 45 giây."),
                new ChallengeSpec("sprint_exam", SubjectId.Sprint, ChallengeKind.Exam,
                    150f, 40f, 0, false, ChallengeDifficulty.Normal, false,
                    "Chạy 150 m trong tối đa 40 giây."),
                new ChallengeSpec("volleyball_learn", SubjectId.Volleyball, ChallengeKind.Learn,
                    0f, 0f, 7, false, ChallengeDifficulty.Easy, true,
                    "Đỡ bóng 3 lần, phát bóng 2 lần, nhảy và đánh trả 2 lần."),
                new ChallengeSpec("volleyball_practice", SubjectId.Volleyball, ChallengeKind.Practice,
                    0f, 0f, 5, false, ChallengeDifficulty.Easy, true,
                    "Ghi đủ 5 điểm trước đối thủ. Không giới hạn thời gian."),
                new ChallengeSpec("volleyball_exam", SubjectId.Volleyball, ChallengeKind.Exam,
                    0f, 0f, 10, false, ChallengeDifficulty.Normal, false,
                    "Ghi đủ 10 điểm trước đối thủ. Không giới hạn thời gian."),
                new ChallengeSpec("soccer_learn", SubjectId.Football, ChallengeKind.Learn,
                    0f, 0f, 3, false, ChallengeDifficulty.Normal, true,
                    "Ghi 3 bàn vào khung thành không có thủ môn. Không giới hạn lượt sút."),
                new ChallengeSpec("soccer_practice", SubjectId.Football, ChallengeKind.Practice,
                    0f, 0f, 2, true, ChallengeDifficulty.Normal, true,
                    "Ghi ít nhất 2 bàn trong tối đa 6 lượt sút. Có thủ môn.", 6),
                new ChallengeSpec("soccer_exam", SubjectId.Football, ChallengeKind.Exam,
                    0f, 0f, 3, true, ChallengeDifficulty.Normal, false,
                    "Sút đủ 5 lượt và ghi ít nhất 3 bàn. Có thủ môn."),
                new ChallengeSpec("chess_final", SubjectId.Chess, ChallengeKind.Final,
                    0f, 90f, 2, false, ChallengeDifficulty.Normal, false,
                    "Giải thế cờ ngẫu nhiên. Chiếu hết trong số nước quy định theo độ khó.")
            };

            var definitions = new ChallengeDefinition[specs.Length];
            for (int i = 0; i < specs.Length; i++)
            {
                ChallengeSpec spec = specs[i];
                string path = $"{ChallengeFolder}/{spec.Id}.asset";
                ChallengeDefinition definition = AssetDatabase.LoadAssetAtPath<ChallengeDefinition>(path);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<ChallengeDefinition>();
                    AssetDatabase.CreateAsset(definition, path);
                }

                Apply(definition, spec);
                definitions[i] = definition;
            }

            ChallengeCatalog catalog = AssetDatabase.LoadAssetAtPath<ChallengeCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ChallengeCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var serializedCatalog = new SerializedObject(catalog);
            SerializedProperty challenges = serializedCatalog.FindProperty("challenges");
            challenges.arraySize = definitions.Length;
            for (int i = 0; i < definitions.Length; i++)
            {
                challenges.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
            }
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();

            if (!catalog.Validate(out string error))
            {
                throw new InvalidOperationException($"Generated challenge catalog is invalid: {error}");
            }

            EditorUtility.SetDirty(catalog);
            BuildSprintBalance();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[KMA] Student journey challenge catalog built.");
        }

        [MenuItem("KMA/Journey/Build Sprint Balance")]
        public static void BuildSprintBalance()
        {
            EnsureFolder(ResourcesFolder);
            SprintBalanceConfig config = AssetDatabase.LoadAssetAtPath<SprintBalanceConfig>(SprintBalancePath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<SprintBalanceConfig>();
                AssetDatabase.CreateAsset(config, SprintBalancePath);
            }

            SprintBalanceParameters value = SprintBalanceParameters.Default;
            SerializedObject serialized = new SerializedObject(config);
            serialized.FindProperty("correctImpulse").floatValue = value.CorrectImpulse;
            serialized.FindProperty("wrongImpulseFactor").floatValue = value.WrongImpulseFactor;
            serialized.FindProperty("speedCap").floatValue = value.SpeedCap;
            serialized.FindProperty("dragPerSecond").floatValue = value.DragPerSecond;
            serialized.FindProperty("distanceScale").floatValue = value.DistanceScale;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        const string CharacterArtFolder = "Assets/_Project/Art/Characters";
        const string TanThu = "tan_thu", Mai = "mai_toang", AnhKhoaTren = "anh_khoa_tren", Co = "co_the_chat";

        static readonly (DialoguePose Pose, string Suffix)[] PoseFiles =
        {
            (DialoguePose.Idle, "idle"), (DialoguePose.Cheer, "cheer0"), (DialoguePose.Hurt, "hurt"),
            (DialoguePose.Jump, "jump"), (DialoguePose.Duck, "duck")
        };

        // The hero and the teacher have their own reaction art; everyone else draws the shared poses.
        static readonly Dictionary<string, Dictionary<DialoguePose, string>> OwnPoseFiles =
            new Dictionary<string, Dictionary<DialoguePose, string>>
            {
                [CharacterArt.Hero] = new Dictionary<DialoguePose, string>
                    { [DialoguePose.Idle] = "fear", [DialoguePose.Cheer] = "happy", [DialoguePose.Hurt] = "disappointed" },
                [CharacterArt.Boss] = new Dictionary<DialoguePose, string>
                    { [DialoguePose.Idle] = "strictLook", [DialoguePose.Cheer] = "scoreWrite", [DialoguePose.Hurt] = "angry" }
            };

        const string EmojiSourceFolder = "Assets/_Project/Art/Emoji/Source~";
        const string EmojiFolder = "Assets/_Project/Art/Emoji";
        const string EmojiAtlasPath = EmojiFolder + "/JourneyEmojiAtlas.png";
        const string EmojiAssetPath = ResourcesFolder + "/JourneyEmoji.asset";
        const int EmojiCell = 72;
        const int EmojiColumns = 5;

        [MenuItem("KMA/Journey/Build Emoji Sprites")]
        public static void BuildEmojiSpriteAsset()
        {
            EnsureFolder(EmojiFolder);
            EnsureFolder(ResourcesFolder);
            IReadOnlyList<string> names = DialogueEmoji.KnownNames;
            int rows = (names.Count + EmojiColumns - 1) / EmojiColumns;
            var atlas = new Texture2D(EmojiColumns * EmojiCell, rows * EmojiCell, TextureFormat.RGBA32, false);
            atlas.SetPixels32(new Color32[atlas.width * atlas.height]);
            var rects = new RectInt[names.Count];
            for (int i = 0; i < names.Count; i++)
            {
                string source = $"{EmojiSourceFolder}/{names[i]}.png";
                if (!File.Exists(source)) throw new InvalidOperationException("Missing emoji source: " + source);
                var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!image.LoadImage(File.ReadAllBytes(source)) || image.width != EmojiCell || image.height != EmojiCell)
                    throw new InvalidOperationException($"Emoji source must be a {EmojiCell}x{EmojiCell} PNG: {source}");
                int x = i % EmojiColumns * EmojiCell;
                int y = atlas.height - (i / EmojiColumns + 1) * EmojiCell;
                atlas.SetPixels32(x, y, EmojiCell, EmojiCell, image.GetPixels32());
                rects[i] = new RectInt(x, y, EmojiCell, EmojiCell);
                UnityEngine.Object.DestroyImmediate(image);
            }
            File.WriteAllBytes(EmojiAtlasPath, atlas.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(atlas);

            AssetDatabase.ImportAsset(EmojiAtlasPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(EmojiAtlasPath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.isReadable = false;
            importer.SaveAndReimport();
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(EmojiAtlasPath);

            TMP_SpriteAsset asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(EmojiAssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                AssetDatabase.CreateAsset(asset, EmojiAssetPath);
            }
            asset.spriteSheet = sheet;
            if (asset.material == null)
            {
                var material = new Material(Shader.Find("TextMeshPro/Sprite")) { name = "JourneyEmoji Material" };
                AssetDatabase.AddObjectToAsset(material, asset);
                asset.material = material;
            }
            asset.material.SetTexture(ShaderUtilities.ID_MainTex, sheet);

            // TMP treats an empty version as a legacy asset and rebuilds (empties) the tables on first
            // access, and throws while doing so. Stamp the version before touching any table.
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("m_Version").stringValue = "1.1.0";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            asset.spriteGlyphTable.Clear();
            asset.spriteCharacterTable.Clear();
            for (int i = 0; i < names.Count; i++)
            {
                var glyph = new TMP_SpriteGlyph((uint)i,
                    new GlyphMetrics(EmojiCell, EmojiCell, 0f, EmojiCell * .8f, EmojiCell),
                    new GlyphRect(rects[i].x, rects[i].y, EmojiCell, EmojiCell), 1f, 0);
                asset.spriteGlyphTable.Add(glyph);
                asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, glyph) { name = names[i] });
            }
            asset.UpdateLookupTables();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log("[KMA] Journey emoji sprite asset built.");
        }

        [MenuItem("KMA/Journey/Build Dialogue Library")]
        public static void BuildDialogues()
        {
            // New dialogue art must be imported as sprites before it can be loaded below.
            CharacterArt.ImportAll();
            EnsureFolder(ResourcesFolder);
            BuildEmojiSpriteAsset();
            JourneyDialogueLibrary library = AssetDatabase.LoadAssetAtPath<JourneyDialogueLibrary>(DialoguePath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<JourneyDialogueLibrary>();
                AssetDatabase.CreateAsset(library, DialoguePath);
            }

            JourneyCharacter[] cast =
            {
                Character(TanThu, "SV năm 1", "#FFC928", true, "MaleAdventurer"),
                Character(Mai, "Mai", "#FF8FB1", false, "FemalePerson"),
                Character(AnhKhoaTren, "Anh Tuấn", "#7FD1FF", false, "MalePerson"),
                Character(Co, "Giảng viên thể chất", "#B9F27C", false, "BossPE")
            };
            JourneyDialogueNode[] nodes =
            {
                Node("opening",
                    Line(AnhKhoaTren, DialoguePose.Idle, "Ôi em ơi, môn này là nỗi ác mộng của sinh viên trường rồi. Chúc em may mắn. Cố lên, rồi sẽ được Bật cóc thôi."),
                    Line(Mai, DialoguePose.Idle, "Tớ nghe các anh chị dọa mãi mà vẫn không tin. Nhìn cô là thấy có chuyện rồi."),
                    Line(TanThu, DialoguePose.Idle, "Tớ còn chưa biết phải chạy thế nào. Chỉ mong đừng bị nợ môn ngay kỳ đầu."),
                    Line(Co, DialoguePose.Idle, "Các cô các cậu đã bước vào đây học thể chất thì phải nghe theo hiệu lệnh của tôi. Lớ ngớ tôi cho nợ môn bất cứ lúc nào.")),
                Node("sprint_intro",
                    Line(Co, DialoguePose.Idle, "Khởi động cho kỹ. Khi nghe còi hiệu lệnh, tất cả chạy."),
                    Line(Mai, DialoguePose.Idle, "Cô bắt chạy mệt thật. Tớ mới đứng ở vạch mà đã thấy chân run rồi."),
                    Line(TanThu, DialoguePose.Idle, "Hết hồn. Tớ còn chưa chạy mà đã sợ không theo kịp cả lớp.")),
                Node("sprint_exam",
                    Line(Co, DialoguePose.Idle, "Đối với bài thi này, cậu phải biết giữ nhịp chạy đều hai chân. Không làm được nữa thì học lại với khóa sau đi. 150 mét, tối đa 40 giây.", "40 GIÂY"),
                    Line(TanThu, DialoguePose.Idle, "Dạ... em sẽ cố giữ nhịp. Cô đừng thổi còi sớm quá ạ.")),
                Node("sprint_pass",
                    Line(Co, DialoguePose.Cheer, "Đạt. Đừng tưởng qua được một môn đã là tài. Sẽ còn gặp tôi dài dài.", "ĐẠT"),
                    Line(Mai, DialoguePose.Idle, "Hết hồn. Tí nữa tớ tưởng không qua. Cô bắt chạy mệt quá.")),
                Node("volleyball_intro",
                    Line(Mai, DialoguePose.Idle, "Bóng chuyền còn mệt hơn chạy đấy. Đỡ không kịp là bóng rơi ngay trước mặt."),
                    Line(Co, DialoguePose.Idle, "Bóng sang thì di chuyển đến đúng vị trí, đỡ bóng và đánh bóng lại cho đối phương. Chỉ biết nhìn bóng thì dĩ nhiên là nợ."),
                    Line(TanThu, DialoguePose.Idle, "Em chưa quen đỡ bóng kiểu này...")),
                Node("volleyball_exam",
                    Line(Co, DialoguePose.Idle, "Bên nào đủ 10 điểm trước thì qua môn. Không đạt thì lại ra mà bật cóc.", "10 ĐIỂM"),
                    Line(Mai, DialoguePose.Idle, "Cứ nhìn bóng mà đỡ. Đừng cuống lên vì một điểm, tớ cũng đang run đây."),
                    Line(TanThu, DialoguePose.Idle, "Tớ sợ bóng bay thẳng vào người rồi không kịp đỡ ấy chứ. Đúng là ác mộng.")),
                Node("volleyball_pass",
                    Line(Co, DialoguePose.Cheer, "Cậu này qua.", "ĐẠT"),
                    Line(TanThu, DialoguePose.Cheer, "Hết hồn. Hai môn rồi mà tớ vẫn chưa quen với cách cô nhìn cả sân.")),
                Node("soccer_intro",
                    Line(Mai, DialoguePose.Idle, "Đến bóng đá rồi. Tớ chỉ sợ sút mãi không vào, nhìn thủ môn là mất bình tĩnh."),
                    Line(AnhKhoaTren, DialoguePose.Idle, "Năm ngoái anh chủ quan. Cứ tưởng sút mạnh là được, cuối cùng bóng bay qua khung thành."),
                    Line(Co, DialoguePose.Idle, "Cậu có đúng năm lượt. Sút ra ngoài cũng tính một lượt, đừng mong tôi cho thêm.")),
                Node("soccer_exam",
                    Line(Co, DialoguePose.Idle, "Năm lượt sút, ghi được ít nhất ba bàn thì qua.", "5 LƯỢT"),
                    Line(TanThu, DialoguePose.Idle, "Dạ... em sẽ cố không sút thẳng vào thủ môn.")),
                Node("soccer_pass",
                    Line(Co, DialoguePose.Cheer, "Đạt. Đừng tưởng ba môn qua là xong. Cậu vẫn còn một bài kiểm tra cuối cùng.", "ĐẠT"),
                    Line(Mai, DialoguePose.Idle, "Còn một bài nữa thôi. Cố lên nào!")),
                Node("chess_intro",
                    Line(Co, DialoguePose.Idle, "Bài này cho một thế cờ, cậu mà không chiếu hết được trong số nước quy định thì tự giác mà bật cóc.", "BÀI CUỐI"),
                    Line(TanThu, DialoguePose.Idle, "Dạ, cờ vua ạ? Em tưởng qua bóng đá là được về rồi."),
                    Line(Co, DialoguePose.Idle, "Tôi cho cậu đúng 90 giây suy nghĩ. Tên cậu là gì để tôi ghi vào sổ luôn?", "90 GIÂY")),
                Node("supplementary",
                    Line(Co, DialoguePose.Idle, "Phần đã đạt tôi giữ nguyên. Phần chưa đạt thì học lại. Học lại tôi vẫn chấm đúng yêu cầu cũ, đừng có mong dễ hơn."),
                    Line(Mai, DialoguePose.Idle, "Cậu vướng chỗ nào thì tập lại chỗ đó. Năm ngoái anh Tuấn cũng bị phạt vậy, không ai thoát được đâu.")),
                Node("course_complete",
                    Line(Co, DialoguePose.Cheer, "Tốt, cậu này được. Đạt học phần. Đừng tưởng qua được hết hôm nay thì năm sau khỏi gặp tôi.", "HOÀN THÀNH"),
                    Line(TanThu, DialoguePose.Cheer, "Hết hồn... em tưởng mình phải học lại thật."),
                    Line(AnhKhoaTren, DialoguePose.Idle, "Chúc mừng em. Anh nói rồi, môn này không thoát được đâu. Năm ngoái anh cũng bị phạt Bật cóc."),
                    Line(Mai, DialoguePose.Idle, "Đi thôi. Tớ đói từ lúc cô bắt chạy đến giờ. Đúng là ác mộng."))
            };

            library.SetContent(cast, nodes);
            if (!library.Validate(out string error))
                throw new InvalidOperationException("Generated journey dialogue library is invalid: " + error);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            // Unity writes a space after empty serialized strings; keep the generated YAML clean.
            File.WriteAllText(DialoguePath, string.Join("\n", File.ReadAllLines(DialoguePath)
                .Select(line => line.TrimEnd())) + "\n");
            Debug.Log("[KMA] Student journey dialogue library built.");
        }

        static JourneyCharacter Character(string id, string displayName, string hex, bool isPlayer, string spriteSet)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out Color color))
                throw new InvalidOperationException("Invalid tag color " + hex);
            var poses = new List<JourneyPoseSprite>();
            foreach ((DialoguePose pose, string suffix) in PoseFiles)
            {
                string file = OwnPoseFiles.TryGetValue(spriteSet, out var own) && own.TryGetValue(pose, out string mine)
                    ? mine : suffix;
                string path = $"{CharacterArtFolder}/{spriteSet}/{spriteSet}_{file}.png";
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) throw new InvalidOperationException("Missing dialogue sprite: " + path);
                poses.Add(new JourneyPoseSprite(pose, sprite));
            }
            return new JourneyCharacter(id, displayName, color, isPlayer, poses);
        }

        static JourneyDialogueNode Node(string id, params JourneyDialogueLine[] lines) =>
            new JourneyDialogueNode(id, new List<JourneyDialogueLine>(lines));

        static JourneyDialogueLine Line(string characterId, DialoguePose pose, string text, string sticker = "") =>
            new JourneyDialogueLine(characterId, pose, text, sticker);

        [MenuItem("KMA/Journey/Build All Journey Content")]
        public static void BuildAll()
        {
            BuildChallenges();
            BuildSprintBalance();
            BuildDialogues();
            GameplayDialogueContentBuilder.Build();
            ShellSceneAuthoring.Apply();
        }

        static void Apply(ChallengeDefinition definition, ChallengeSpec spec)
        {
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = spec.Id;
            serialized.FindProperty("subject").intValue = (int)spec.Subject;
            serialized.FindProperty("kind").intValue = (int)spec.Kind;
            serialized.FindProperty("distance").floatValue = spec.Distance;
            serialized.FindProperty("timeLimit").floatValue = spec.TimeLimit;
            serialized.FindProperty("targetCount").intValue = spec.TargetCount;
            serialized.FindProperty("attemptLimit").intValue = spec.AttemptLimit;
            serialized.FindProperty("keeperEnabled").boolValue = spec.KeeperEnabled;
            serialized.FindProperty("difficulty").intValue = (int)spec.Difficulty;
            serialized.FindProperty("timingHelp").boolValue = spec.TimingHelp;
            serialized.FindProperty("objective").stringValue = spec.Objective;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
