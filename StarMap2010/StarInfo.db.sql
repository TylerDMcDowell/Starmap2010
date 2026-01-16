BEGIN TRANSACTION;
CREATE TABLE IF NOT EXISTS "attribute_dictionary" (
	"attr_key"	TEXT,
	"display_name"	TEXT NOT NULL,
	"description"	TEXT,
	"value_kind"	TEXT NOT NULL,
	"units"	TEXT,
	"category"	TEXT,
	"example_value"	TEXT,
	"is_core"	INTEGER NOT NULL DEFAULT 0,
	"is_deprecated"	INTEGER NOT NULL DEFAULT 0,
	"created_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	"updated_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	PRIMARY KEY("attr_key")
);
CREATE TABLE IF NOT EXISTS "attribute_dictionary_aliases" (
	"attr_key"	TEXT NOT NULL,
	"alias"	TEXT NOT NULL,
	PRIMARY KEY("attr_key","alias"),
	FOREIGN KEY("attr_key") REFERENCES "attribute_dictionary"("attr_key") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "body_environment" (
	"object_id"	TEXT,
	"env_stage"	TEXT NOT NULL DEFAULT 'pristine',
	"atmosphere_type"	TEXT NOT NULL DEFAULT 'none',
	"pressure_atm"	REAL,
	"avg_temp_c"	REAL,
	"hydrosphere_pct"	REAL,
	"biosphere"	TEXT NOT NULL DEFAULT 'sterile',
	"radiation_level"	TEXT,
	"magnetosphere"	TEXT,
	"habitability"	TEXT NOT NULL DEFAULT 'none',
	"notes"	TEXT,
	"created_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	"updated_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	PRIMARY KEY("object_id"),
	FOREIGN KEY("object_id") REFERENCES "system_objects"("object_id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "body_species_habitability" (
	"object_id"	TEXT NOT NULL,
	"species_id"	TEXT NOT NULL,
	"score"	REAL NOT NULL DEFAULT 0,
	"rating"	TEXT NOT NULL DEFAULT 'unknown',
	"reason_tags"	TEXT,
	"notes"	TEXT,
	"created_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	"updated_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	PRIMARY KEY("object_id","species_id"),
	FOREIGN KEY("object_id") REFERENCES "system_objects"("object_id") ON DELETE CASCADE,
	FOREIGN KEY("species_id") REFERENCES "species"("species_id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "env_profile_attributes" (
	"profile_id"	TEXT NOT NULL,
	"attr_key"	TEXT NOT NULL,
	"value_text"	TEXT,
	"value_num"	REAL,
	"value_int"	INTEGER,
	"value_bool"	INTEGER,
	"notes"	TEXT,
	PRIMARY KEY("profile_id","attr_key"),
	FOREIGN KEY("profile_id") REFERENCES "species_env_profiles"("profile_id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "governments" (
	"government_id"	TEXT,
	"government_name"	TEXT NOT NULL UNIQUE,
	"faction_color"	TEXT,
	PRIMARY KEY("government_id")
);
CREATE TABLE IF NOT EXISTS "installation_details" (
	"object_id"	TEXT,
	"installation_type"	TEXT NOT NULL,
	"owner_government_id"	TEXT,
	"status"	TEXT NOT NULL DEFAULT 'active',
	"strategic_role"	TEXT,
	"is_primary"	INTEGER NOT NULL DEFAULT 0,
	"staff_count"	INTEGER,
	"security_level"	TEXT,
	"is_secret"	INTEGER NOT NULL DEFAULT 0,
	"commissioned_date"	TEXT,
	"decommissioned_date"	TEXT,
	"notes"	TEXT,
	"created_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	"updated_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	PRIMARY KEY("object_id"),
	FOREIGN KEY("object_id") REFERENCES "system_objects"("object_id") ON DELETE CASCADE,
	FOREIGN KEY("owner_government_id") REFERENCES "governments"("government_id")
);
CREATE TABLE IF NOT EXISTS "jump_gate_links" (
	"link_id"	TEXT,
	"gate_a_id"	TEXT NOT NULL,
	"gate_b_id"	TEXT NOT NULL,
	"status"	TEXT NOT NULL DEFAULT 'open',
	"notes"	TEXT,
	"active_from"	TEXT,
	"active_until"	TEXT,
	"is_bidirectional"	INTEGER NOT NULL DEFAULT 1,
	"transit_hours"	REAL,
	"toll_credits"	INTEGER,
	UNIQUE("gate_a_id","gate_b_id"),
	PRIMARY KEY("link_id"),
	CHECK("gate_a_id" <> "gate_b_id")
);
CREATE TABLE IF NOT EXISTS "jump_gates" (
	"gate_id"	TEXT,
	"system_id"	TEXT NOT NULL,
	"owner_government_id"	TEXT NOT NULL,
	"gate_type"	TEXT NOT NULL,
	"notes"	TEXT,
	"gate_name"	TEXT,
	"gate_class"	TEXT NOT NULL DEFAULT 'standard',
	"gate_role"	TEXT NOT NULL DEFAULT 'standard',
	"commissioned_date"	TEXT,
	"decommissioned_date"	TEXT,
	"is_operational"	INTEGER NOT NULL DEFAULT 1,
	"is_primary"	INTEGER NOT NULL DEFAULT 0,
	PRIMARY KEY("gate_id"),
	FOREIGN KEY("owner_government_id") REFERENCES "governments"("government_id"),
	FOREIGN KEY("system_id") REFERENCES "star_systems"("system_id")
);
CREATE TABLE IF NOT EXISTS "moon_details" (
	"object_id"	TEXT,
	"moon_class"	TEXT,
	"radius_km"	REAL,
	"mass_earth"	REAL,
	"gravity_g"	REAL,
	"day_length_hours"	REAL,
	"tidally_locked"	INTEGER NOT NULL DEFAULT 0,
	"orbital_period_days"	REAL,
	"semi_major_axis_km"	REAL,
	"eccentricity"	REAL,
	"density_g_cm3"	REAL,
	"population"	INTEGER,
	"tech_level"	TEXT,
	"notes"	TEXT,
	"created_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	"updated_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	PRIMARY KEY("object_id"),
	FOREIGN KEY("object_id") REFERENCES "system_objects"("object_id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "object_attributes" (
	"object_id"	TEXT NOT NULL,
	"attr_key"	TEXT NOT NULL,
	"value_text"	TEXT,
	"value_num"	REAL,
	"value_int"	INTEGER,
	"value_bool"	INTEGER,
	"notes"	TEXT,
	"created_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	"updated_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	PRIMARY KEY("object_id","attr_key"),
	FOREIGN KEY("object_id") REFERENCES "system_objects"("object_id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "object_links" (
	"link_id"	TEXT,
	"a_object_id"	TEXT NOT NULL,
	"b_object_id"	TEXT NOT NULL,
	"link_type"	TEXT NOT NULL,
	"status"	TEXT NOT NULL DEFAULT 'open',
	"notes"	TEXT,
	"active_from"	TEXT,
	"active_until"	TEXT,
	"is_bidirectional"	INTEGER NOT NULL DEFAULT 1,
	"transit_hours"	REAL,
	"toll_credits"	INTEGER,
	"created_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	"updated_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	PRIMARY KEY("link_id"),
	FOREIGN KEY("a_object_id") REFERENCES "system_objects"("object_id") ON DELETE CASCADE,
	FOREIGN KEY("b_object_id") REFERENCES "system_objects"("object_id") ON DELETE CASCADE,
	CHECK("a_object_id" <> "b_object_id")
);
CREATE TABLE IF NOT EXISTS "planet_details" (
	"object_id"	TEXT,
	"planet_class"	TEXT,
	"radius_km"	REAL,
	"mass_earth"	REAL,
	"gravity_g"	REAL,
	"day_length_hours"	REAL,
	"axial_tilt_deg"	REAL,
	"semi_major_axis_au"	REAL,
	"orbital_period_days"	REAL,
	"eccentricity"	REAL,
	"albedo"	REAL,
	"density_g_cm3"	REAL,
	"population"	INTEGER,
	"tech_level"	TEXT,
	"notes"	TEXT,
	"created_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	"updated_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	PRIMARY KEY("object_id"),
	FOREIGN KEY("object_id") REFERENCES "system_objects"("object_id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "species" (
	"species_id"	TEXT,
	"species_name"	TEXT NOT NULL UNIQUE,
	"is_playable"	INTEGER NOT NULL DEFAULT 0,
	"notes"	TEXT,
	PRIMARY KEY("species_id")
);
CREATE TABLE IF NOT EXISTS "species_attributes" (
	"species_id"	TEXT NOT NULL,
	"attr_key"	TEXT NOT NULL,
	"value_text"	TEXT,
	"value_num"	REAL,
	"value_int"	INTEGER,
	"value_bool"	INTEGER,
	"notes"	TEXT,
	"created_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	"updated_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	PRIMARY KEY("species_id","attr_key"),
	FOREIGN KEY("species_id") REFERENCES "species"("species_id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "species_env_profiles" (
	"profile_id"	TEXT,
	"species_id"	TEXT NOT NULL,
	"profile_name"	TEXT NOT NULL,
	"is_default"	INTEGER NOT NULL DEFAULT 0,
	"notes"	TEXT,
	"created_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	"updated_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	PRIMARY KEY("profile_id"),
	FOREIGN KEY("species_id") REFERENCES "species"("species_id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "sqlite_stat4" (
	"tbl"	,
	"idx"	,
	"neq"	,
	"nlt"	,
	"ndlt"	,
	"sample"	
);
CREATE TABLE IF NOT EXISTS "star_systems" (
	"system_id"	TEXT,
	"system_name"	TEXT NOT NULL,
	"real_system_name"	TEXT,
	"primary_star_name"	TEXT,
	"primary_star_type"	TEXT,
	"government_id"	TEXT NOT NULL,
	"real_x_ly"	REAL NOT NULL,
	"real_y_ly"	REAL NOT NULL,
	"real_z_ly"	REAL NOT NULL,
	"distance_ly"	REAL NOT NULL,
	"ra_deg"	REAL NOT NULL,
	"dec_deg"	REAL NOT NULL,
	"screen_x"	INTEGER NOT NULL,
	"screen_y"	INTEGER NOT NULL,
	"system_type"	TEXT NOT NULL,
	"strategic_value"	TEXT NOT NULL,
	"habitability_class"	TEXT NOT NULL,
	"has_stations"	INTEGER NOT NULL DEFAULT 0,
	"has_gates"	INTEGER NOT NULL DEFAULT 0,
	"notes"	TEXT,
	PRIMARY KEY("system_id"),
	FOREIGN KEY("government_id") REFERENCES "governments"("government_id")
);
CREATE TABLE IF NOT EXISTS "star_types" (
	"star_type_code"	TEXT,
	"spectral_class"	TEXT NOT NULL,
	"subclass"	INTEGER,
	"luminosity_class"	TEXT NOT NULL,
	"display_color"	TEXT NOT NULL,
	"color_hex"	TEXT NOT NULL,
	"surface_temp_k_min"	INTEGER,
	"surface_temp_k_max"	INTEGER,
	"mass_sol_min"	REAL,
	"mass_sol_max"	REAL,
	"luminosity_sol_min"	REAL,
	"luminosity_sol_max"	REAL,
	"lifespan_gyr_min"	REAL,
	"lifespan_gyr_max"	REAL,
	"habitability_bias"	TEXT,
	"notes"	TEXT,
	"star_key"	TEXT,
	PRIMARY KEY("star_type_code")
);
CREATE TABLE IF NOT EXISTS "system_bodies" (
	"body_id"	TEXT,
	"system_id"	TEXT NOT NULL,
	"body_name"	TEXT NOT NULL,
	"body_kind"	TEXT NOT NULL,
	"body_class"	TEXT,
	"habitability_class"	TEXT,
	"notes"	TEXT,
	PRIMARY KEY("body_id"),
	FOREIGN KEY("system_id") REFERENCES "star_systems"("system_id")
);
CREATE TABLE IF NOT EXISTS "system_claim_history" (
	"claim_id"	INTEGER,
	"system_id"	TEXT NOT NULL,
	"government_id"	TEXT NOT NULL,
	"claim_type"	TEXT NOT NULL,
	"claim_start"	TEXT,
	"claim_end"	TEXT,
	"notes"	TEXT,
	PRIMARY KEY("claim_id" AUTOINCREMENT),
	FOREIGN KEY("government_id") REFERENCES "governments"("government_id"),
	FOREIGN KEY("system_id") REFERENCES "star_systems"("system_id")
);
CREATE TABLE IF NOT EXISTS "system_installations" (
	"installation_id"	INTEGER,
	"system_id"	TEXT NOT NULL,
	"installation_name"	TEXT NOT NULL,
	"installation_type"	TEXT NOT NULL,
	"owner_government_id"	TEXT NOT NULL,
	"is_primary"	INTEGER NOT NULL DEFAULT 0,
	"strategic_role"	TEXT NOT NULL,
	"notes"	TEXT,
	PRIMARY KEY("installation_id" AUTOINCREMENT),
	FOREIGN KEY("owner_government_id") REFERENCES "governments"("government_id"),
	FOREIGN KEY("system_id") REFERENCES "star_systems"("system_id")
);
CREATE TABLE IF NOT EXISTS "system_objects" (
	"object_id"	TEXT,
	"system_id"	TEXT NOT NULL,
	"object_kind"	TEXT NOT NULL,
	"parent_object_id"	TEXT,
	"orbit_host_object_id"	TEXT,
	"radial_order"	INTEGER NOT NULL DEFAULT 0,
	"display_name"	TEXT NOT NULL,
	"notes"	TEXT,
	"related_table"	TEXT,
	"related_id"	TEXT,
	"flags"	TEXT,
	"created_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	"updated_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	PRIMARY KEY("object_id"),
	FOREIGN KEY("orbit_host_object_id") REFERENCES "system_objects"("object_id") ON DELETE SET NULL,
	FOREIGN KEY("parent_object_id") REFERENCES "system_objects"("object_id") ON DELETE CASCADE,
	FOREIGN KEY("system_id") REFERENCES "star_systems"("system_id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "system_stars" (
	"star_id"	INTEGER,
	"system_id"	TEXT NOT NULL,
	"star_name"	TEXT NOT NULL,
	"real_star_name"	TEXT NOT NULL,
	"star_type"	TEXT NOT NULL,
	"spectral_class"	TEXT NOT NULL,
	"orbital_role"	TEXT NOT NULL,
	"semi_major_axis_au"	REAL,
	"eccentricity"	REAL,
	PRIMARY KEY("star_id" AUTOINCREMENT),
	FOREIGN KEY("system_id") REFERENCES "star_systems"("system_id")
);
CREATE TABLE IF NOT EXISTS "terraform_constraints" (
	"object_id"	TEXT,
	"terraform_tier"	TEXT NOT NULL DEFAULT 'unknown',
	"atmosphere_retention"	TEXT NOT NULL DEFAULT 'unknown',
	"radiation_constraint"	TEXT NOT NULL DEFAULT 'unknown',
	"volatile_budget"	TEXT NOT NULL DEFAULT 'unknown',
	"water_availability"	TEXT NOT NULL DEFAULT 'unknown',
	"requires_imports"	TEXT,
	"limiting_factors"	TEXT,
	"maintenance_burden"	TEXT NOT NULL DEFAULT 'unknown',
	"notes"	TEXT,
	"created_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	"updated_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	PRIMARY KEY("object_id"),
	FOREIGN KEY("object_id") REFERENCES "system_objects"("object_id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "terraform_projects" (
	"project_id"	TEXT,
	"object_id"	TEXT NOT NULL,
	"project_name"	TEXT NOT NULL,
	"status"	TEXT NOT NULL DEFAULT 'planned',
	"approach"	TEXT,
	"initiator_government_id"	TEXT,
	"start_year"	INTEGER,
	"end_year"	INTEGER,
	"progress_pct"	REAL NOT NULL DEFAULT 0,
	"notes"	TEXT,
	"created_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	"updated_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	PRIMARY KEY("project_id"),
	FOREIGN KEY("initiator_government_id") REFERENCES "governments"("government_id"),
	FOREIGN KEY("object_id") REFERENCES "system_objects"("object_id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "terraform_target_attributes" (
	"target_id"	TEXT NOT NULL,
	"attr_key"	TEXT NOT NULL,
	"value_text"	TEXT,
	"value_num"	REAL,
	"value_int"	INTEGER,
	"value_bool"	INTEGER,
	"notes"	TEXT,
	PRIMARY KEY("target_id","attr_key"),
	FOREIGN KEY("target_id") REFERENCES "terraform_targets"("target_id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "terraform_targets" (
	"target_id"	TEXT,
	"target_name"	TEXT NOT NULL UNIQUE,
	"species_id"	TEXT,
	"profile_id"	TEXT,
	"notes"	TEXT,
	"created_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	"updated_utc"	TEXT NOT NULL DEFAULT (datetime('now')),
	PRIMARY KEY("target_id"),
	FOREIGN KEY("profile_id") REFERENCES "species_env_profiles"("profile_id") ON DELETE SET NULL,
	FOREIGN KEY("species_id") REFERENCES "species"("species_id") ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS "ix_attr_dict_alias" ON "attribute_dictionary_aliases" (
	"alias"
);
CREATE INDEX IF NOT EXISTS "ix_attr_dict_category" ON "attribute_dictionary" (
	"category"
);
CREATE INDEX IF NOT EXISTS "ix_attr_dict_core" ON "attribute_dictionary" (
	"is_core"
);
CREATE INDEX IF NOT EXISTS "ix_attr_dict_kind" ON "attribute_dictionary" (
	"value_kind"
);
CREATE INDEX IF NOT EXISTS "ix_body_environment_habitability" ON "body_environment" (
	"habitability"
);
CREATE INDEX IF NOT EXISTS "ix_body_environment_stage" ON "body_environment" (
	"env_stage"
);
CREATE INDEX IF NOT EXISTS "ix_body_species_hab_rating" ON "body_species_habitability" (
	"rating"
);
CREATE INDEX IF NOT EXISTS "ix_body_species_hab_species" ON "body_species_habitability" (
	"species_id"
);
CREATE INDEX IF NOT EXISTS "ix_env_profile_attributes_key" ON "env_profile_attributes" (
	"attr_key"
);
CREATE INDEX IF NOT EXISTS "ix_installation_details_owner" ON "installation_details" (
	"owner_government_id"
);
CREATE INDEX IF NOT EXISTS "ix_installation_details_status" ON "installation_details" (
	"status"
);
CREATE INDEX IF NOT EXISTS "ix_installation_details_type" ON "installation_details" (
	"installation_type"
);
CREATE INDEX IF NOT EXISTS "ix_jump_gates_system" ON "jump_gates" (
	"system_id"
);
CREATE INDEX IF NOT EXISTS "ix_moon_details_class" ON "moon_details" (
	"moon_class"
);
CREATE INDEX IF NOT EXISTS "ix_object_attributes_key" ON "object_attributes" (
	"attr_key"
);
CREATE INDEX IF NOT EXISTS "ix_object_attributes_object" ON "object_attributes" (
	"object_id"
);
CREATE INDEX IF NOT EXISTS "ix_object_links_a" ON "object_links" (
	"a_object_id"
);
CREATE INDEX IF NOT EXISTS "ix_object_links_b" ON "object_links" (
	"b_object_id"
);
CREATE INDEX IF NOT EXISTS "ix_object_links_type" ON "object_links" (
	"link_type"
);
CREATE INDEX IF NOT EXISTS "ix_planet_details_class" ON "planet_details" (
	"planet_class"
);
CREATE INDEX IF NOT EXISTS "ix_species_attributes_key" ON "species_attributes" (
	"attr_key"
);
CREATE INDEX IF NOT EXISTS "ix_species_env_profiles_species" ON "species_env_profiles" (
	"species_id"
);
CREATE INDEX IF NOT EXISTS "ix_system_bodies_system" ON "system_bodies" (
	"system_id"
);
CREATE INDEX IF NOT EXISTS "ix_system_objects_orbit" ON "system_objects" (
	"orbit_host_object_id"
);
CREATE INDEX IF NOT EXISTS "ix_system_objects_order" ON "system_objects" (
	"system_id",
	"orbit_host_object_id",
	"radial_order"
);
CREATE INDEX IF NOT EXISTS "ix_system_objects_parent" ON "system_objects" (
	"parent_object_id"
);
CREATE INDEX IF NOT EXISTS "ix_system_objects_system" ON "system_objects" (
	"system_id"
);
CREATE INDEX IF NOT EXISTS "ix_terraform_constraints_retention" ON "terraform_constraints" (
	"atmosphere_retention"
);
CREATE INDEX IF NOT EXISTS "ix_terraform_constraints_tier" ON "terraform_constraints" (
	"terraform_tier"
);
CREATE INDEX IF NOT EXISTS "ix_terraform_projects_object" ON "terraform_projects" (
	"object_id"
);
CREATE INDEX IF NOT EXISTS "ix_terraform_projects_status" ON "terraform_projects" (
	"status"
);
CREATE INDEX IF NOT EXISTS "ix_terraform_target_attributes_key" ON "terraform_target_attributes" (
	"attr_key"
);
CREATE INDEX IF NOT EXISTS "ix_terraform_targets_profile" ON "terraform_targets" (
	"profile_id"
);
CREATE INDEX IF NOT EXISTS "ix_terraform_targets_species" ON "terraform_targets" (
	"species_id"
);
CREATE UNIQUE INDEX IF NOT EXISTS "ux_object_links_pair" ON "object_links" (
	"a_object_id",
	"b_object_id",
	"link_type"
);
CREATE UNIQUE INDEX IF NOT EXISTS "ux_species_env_default" ON "species_env_profiles" (
	"species_id"
) WHERE "is_default" = 1;
CREATE UNIQUE INDEX IF NOT EXISTS "ux_star_systems_screenxy" ON "star_systems" (
	"screen_x",
	"screen_y"
);
CREATE UNIQUE INDEX IF NOT EXISTS "ux_star_systems_system_id" ON "star_systems" (
	"system_id"
);
CREATE UNIQUE INDEX IF NOT EXISTS "ux_star_types_star_key" ON "star_types" (
	"star_key"
);
CREATE UNIQUE INDEX IF NOT EXISTS "ux_system_bodies_system_name_kind" ON "system_bodies" (
	"system_id",
	"body_name",
	"body_kind"
);
CREATE UNIQUE INDEX IF NOT EXISTS "ux_system_objects_related_jump_gates" ON "system_objects" (
	"related_table",
	"related_id"
) WHERE "related_table" = 'jump_gates';
CREATE UNIQUE INDEX IF NOT EXISTS "ux_system_objects_related_star" ON "system_objects" (
	"related_table",
	"related_id"
) WHERE "related_table" = 'system_stars';
CREATE TRIGGER trg_attr_dict_touch
AFTER UPDATE ON attribute_dictionary
FOR EACH ROW
BEGIN
  UPDATE attribute_dictionary
  SET updated_utc = datetime('now')
  WHERE attr_key = OLD.attr_key;
END;
CREATE TRIGGER trg_body_environment_touch
AFTER UPDATE ON body_environment
FOR EACH ROW
BEGIN
    UPDATE body_environment
    SET updated_utc = datetime('now')
    WHERE object_id = OLD.object_id;
END;
CREATE TRIGGER trg_body_species_habitability_touch
AFTER UPDATE ON body_species_habitability
FOR EACH ROW
BEGIN
    UPDATE body_species_habitability
    SET updated_utc = datetime('now')
    WHERE object_id = OLD.object_id AND species_id = OLD.species_id;
END;
CREATE TRIGGER trg_installation_details_touch
AFTER UPDATE ON installation_details
FOR EACH ROW
BEGIN
    UPDATE installation_details
    SET updated_utc = datetime('now')
    WHERE object_id = OLD.object_id;
END;
CREATE TRIGGER trg_moon_details_touch
AFTER UPDATE ON moon_details
FOR EACH ROW
BEGIN
    UPDATE moon_details
    SET updated_utc = datetime('now')
    WHERE object_id = OLD.object_id;
END;
CREATE TRIGGER trg_object_attributes_touch
AFTER UPDATE ON object_attributes
FOR EACH ROW
BEGIN
    UPDATE object_attributes
    SET updated_utc = datetime('now')
    WHERE object_id = OLD.object_id AND attr_key = OLD.attr_key;
END;
CREATE TRIGGER trg_object_links_touch
AFTER UPDATE ON object_links
FOR EACH ROW
BEGIN
    UPDATE object_links
    SET updated_utc = datetime('now')
    WHERE link_id = OLD.link_id;
END;
CREATE TRIGGER trg_planet_details_touch
AFTER UPDATE ON planet_details
FOR EACH ROW
BEGIN
    UPDATE planet_details
    SET updated_utc = datetime('now')
    WHERE object_id = OLD.object_id;
END;
CREATE TRIGGER trg_species_attributes_touch
AFTER UPDATE ON species_attributes
FOR EACH ROW
BEGIN
    UPDATE species_attributes
    SET updated_utc = datetime('now')
    WHERE species_id = OLD.species_id AND attr_key = OLD.attr_key;
END;
CREATE TRIGGER trg_species_env_profiles_touch
AFTER UPDATE ON species_env_profiles
FOR EACH ROW
BEGIN
    UPDATE species_env_profiles
    SET updated_utc = datetime('now')
    WHERE profile_id = OLD.profile_id;
END;
CREATE TRIGGER trg_star_systems_autoset_system_id
BEFORE INSERT ON star_systems
FOR EACH ROW
WHEN NEW.system_id IS NULL OR trim(NEW.system_id) = ''
BEGIN
    SELECT
        NEW.system_id =
            lower(hex(randomblob(4))) || '-' ||
            lower(hex(randomblob(2))) || '-' ||
            lower(hex(randomblob(2))) || '-' ||
            lower(hex(randomblob(2))) || '-' ||
            lower(hex(randomblob(6)));
END;
CREATE TRIGGER trg_star_systems_block_system_id_update
BEFORE UPDATE OF system_id ON star_systems
FOR EACH ROW
WHEN NEW.system_id IS NULL OR trim(NEW.system_id) = '' OR NEW.system_id <> OLD.system_id
BEGIN
    SELECT RAISE(ABORT, 'system_id cannot be changed or cleared');
END;
CREATE TRIGGER trg_system_objects_parent_same_system
BEFORE INSERT ON system_objects
FOR EACH ROW
WHEN NEW.parent_object_id IS NOT NULL
AND (
    SELECT system_id
    FROM system_objects
    WHERE object_id = NEW.parent_object_id
) <> NEW.system_id
BEGIN
    SELECT RAISE(ABORT, 'Parent object must belong to same system');
END;
CREATE TRIGGER trg_system_objects_parent_update_same_system
BEFORE UPDATE OF parent_object_id ON system_objects
FOR EACH ROW
WHEN NEW.parent_object_id IS NOT NULL
AND (
    SELECT system_id
    FROM system_objects
    WHERE object_id = NEW.parent_object_id
) <> NEW.system_id
BEGIN
    SELECT RAISE(ABORT, 'Parent object must belong to same system');
END;
CREATE TRIGGER trg_system_objects_touch
AFTER UPDATE ON system_objects
FOR EACH ROW
BEGIN
    UPDATE system_objects
    SET updated_utc = datetime('now')
    WHERE object_id = OLD.object_id;
END;
CREATE TRIGGER trg_terraform_constraints_touch
AFTER UPDATE ON terraform_constraints
FOR EACH ROW
BEGIN
    UPDATE terraform_constraints
    SET updated_utc = datetime('now')
    WHERE object_id = OLD.object_id;
END;
CREATE TRIGGER trg_terraform_projects_touch
AFTER UPDATE ON terraform_projects
FOR EACH ROW
BEGIN
    UPDATE terraform_projects
    SET updated_utc = datetime('now')
    WHERE project_id = OLD.project_id;
END;
CREATE TRIGGER trg_terraform_targets_touch
AFTER UPDATE ON terraform_targets
FOR EACH ROW
BEGIN
    UPDATE terraform_targets
    SET updated_utc = datetime('now')
    WHERE target_id = OLD.target_id;
END;
COMMIT;
