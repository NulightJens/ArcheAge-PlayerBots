using AAEmu.Commons.Utils.DB;
using MySql.Data.MySqlClient;

namespace AAEmu.Game.Core.Managers.Bots;

internal sealed class MySqlBotArchetypeStore : IBotArchetypeStore
{
    private const string TableName = "bot_archetype_plans";

    public (string archetypeName, bool isFinal) Get(uint characterId)
    {
        using var connection = MySQL.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT archetype_name, is_final FROM `{TableName}` WHERE character_id = @charId";
        command.Parameters.AddWithValue("@charId", characterId);
        using var reader = command.ExecuteReader();
        if (reader.Read())
            return (reader.GetString("archetype_name"), reader.GetBoolean("is_final"));
        return (null, false);
    }

    public void Save(uint characterId, string archetypeName, bool isFinal)
    {
        using var connection = MySQL.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $@"
                INSERT INTO `{TableName}` (character_id, archetype_name, is_final)
                VALUES (@charId, @name, @final)
                ON DUPLICATE KEY UPDATE
                    archetype_name = VALUES(archetype_name),
                    is_final = VALUES(is_final),
                    updated_at = CURRENT_TIMESTAMP";
        command.Parameters.AddWithValue("@charId", characterId);
        command.Parameters.AddWithValue("@name", archetypeName);
        command.Parameters.AddWithValue("@final", isFinal);
        command.ExecuteNonQuery();
    }

    public void Delete(uint characterId)
    {
        using var connection = MySQL.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"DELETE FROM `{TableName}` WHERE character_id = @charId";
        command.Parameters.AddWithValue("@charId", characterId);
        command.ExecuteNonQuery();
    }
}
