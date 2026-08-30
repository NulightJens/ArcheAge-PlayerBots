-- BotPlayers: persisted archetype plan per bot character (planned vs finalized).
CREATE TABLE IF NOT EXISTS `bot_archetype_plans` (
  `character_id` INT UNSIGNED NOT NULL,
  `archetype_name` VARCHAR(64) NOT NULL,
  `is_final` TINYINT(1) NOT NULL DEFAULT '0',
  `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
  `updated_at` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`character_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
