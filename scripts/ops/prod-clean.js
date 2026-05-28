// Clears user-generated content from a FixIt MongoDB, preserving reference
// data (cities, tags, neighborhoods, supportedLanguages) and migration history.
//
// Usage (preview — no changes):
//   DRY_RUN=1 mongosh "$MONGODB_URI" --quiet --file scripts/ops/prod-clean.js
//
// Usage (execute):
//   DRY_RUN=0 mongosh "$MONGODB_URI" --quiet --file scripts/ops/prod-clean.js
//
// After execution, run the admin bootstrap CLI to provision the initial admin:
//   dotnet run --project FixIt/FixIt.csproj -- --bootstrap-admin \
//     --email <e> --password <p>

const dryRun = (process.env.DRY_RUN ?? "1") !== "0";

const collectionsToClear = [
    "AspNetUsers",              // Identity users (test accounts)
    "issues",
    "comments",
    "votes",
    "viewEvents",
    "userReputations",
    "reputationTransactions",
    "leaderboards",
    "hazards",
    "issueAnalyses",
    "adminSuggestions",
    "media",
    "mediaReferences",
    "contentReports",
    "issueResolutionEvidence",
    "translations",
    "officialResponses",
    "moderationActions",
    "audit-logs",
    "sessions",
    "users",                    // legacy empty collection from old migration
];

const collectionsToKeep = [
    "cities",                   // reference data (Bulgarian municipalities)
    "tags",                     // reference data (civic-issue taxonomy)
    "neighborhoods",            // reference data
    "supportedLanguages",       // reference data
    "_migrations",              // migration history — DO NOT clear
];

print(`Database : ${db.getName()}`);
print(`Mode     : ${dryRun ? "DRY-RUN (no changes will be written)" : "EXECUTE (deletions will be performed)"}`);
print("");

const existing = db.getCollectionNames();
const pad = (s) => s.padEnd(28);

print("Collections to clear:");
let totalToDelete = 0;
let totalDeleted = 0;
for (const name of collectionsToClear) {
    if (!existing.includes(name)) {
        print(`  - ${pad(name)} (not present)`);
        continue;
    }
    const count = db.getCollection(name).countDocuments();
    totalToDelete += count;
    if (dryRun) {
        print(`  - ${pad(name)} ${count} docs would be deleted`);
    } else {
        const r = db.getCollection(name).deleteMany({});
        totalDeleted += r.deletedCount;
        print(`  - ${pad(name)} deleted ${r.deletedCount} docs`);
    }
}

print("");
print("Collections preserved:");
for (const name of collectionsToKeep) {
    if (existing.includes(name)) {
        const count = db.getCollection(name).countDocuments();
        print(`  - ${pad(name)} ${count} docs (preserved)`);
    } else {
        print(`  - ${pad(name)} (not present — app will recreate on next start)`);
    }
}

print("");
const unaccounted = existing.filter(
    (n) => !collectionsToClear.includes(n) && !collectionsToKeep.includes(n) && !n.startsWith("system.")
);
if (unaccounted.length) {
    print("⚠ Collections present but not classified (LEFT UNTOUCHED — review):");
    for (const name of unaccounted) {
        const count = db.getCollection(name).countDocuments();
        print(`  - ${pad(name)} ${count} docs`);
    }
} else {
    print("All present collections classified — no surprises.");
}

print("");
if (dryRun) {
    print(`Summary: ${totalToDelete} docs would be deleted across ${collectionsToClear.length} target collections.`);
    print("Re-run with DRY_RUN=0 to actually delete.");
} else {
    print(`Summary: deleted ${totalDeleted} docs across ${collectionsToClear.length} target collections.`);
    print("Done. Now provision the admin via:");
    print("  dotnet run --project FixIt/FixIt.csproj -- --bootstrap-admin --email <e> --password <p>");
}
