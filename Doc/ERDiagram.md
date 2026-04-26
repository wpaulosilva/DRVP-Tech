mermaid
erDiagram

    dbo.User {
        user_id int PK
        username nvarchar(64)
        password_hash nvarchar(256)
        is_active bit
        created_at date
        email nvarchar(254)
        person_info_id int FK
    }

    dbo.Person_Info {
        person_id int PK
        first_name nvarchar(64)
        last_name nvarchar(64)
        birth_date date
        phone varchar(20)
        address nvarchar(128)
        nif varchar(20)
    }

    dbo.Student {
        student_id int PK
        parent_user_id int FK
        person_info_id int FK
        acceptance_status tinyint
        is_active bit
    }

    dbo.Participant {
        participant_id int PK
        id_coach_class int FK
        id_student int FK
        joined_at date
        parent_validated_at datetime2(7)
        validation_status tinyint
    }

    dbo.Coach_Class {
        class_id int PK
        id_modality int FK
        id_studio int FK
        id_coach int FK
        created_by int FK
        start_datetime datetime2(7)
        end_datetime datetime2(7)
        status tinyint
        max_participants int
        created_at date
        coach_validated_at datetime2(7)
        staff_validated_at datetime2(7)
        coach_validation_status tinyint
        finished_at datetime2(7)
    }

    dbo.Coach {
        coach_id int PK "FK to User.user_id (shared PK)"
        biography nvarchar(256)
        photo_url nvarchar(256)
    }

    dbo.Modality {
        modality_id int PK
        name nvarchar(64)
        is_active bit
        description nvarchar(256)
    }

    dbo.Studio {
        studio_id int PK
        name nvarchar(64)
        capacity int
        is_active bit
        address nvarchar(200)
    }

    dbo.Studio_Modality {
        id_studio int FK
        id_modality int FK
    }

    dbo.Coach_Modality {
        id_coach int FK
        id_modality int FK
    }

    dbo.Blocked_Period {
        blocked_id int PK
        start_datetime datetime2(7)
        end_datetime datetime2(7)
        id_coach int FK
        id_studio int FK
        scope tinyint
        reason nvarchar(128)
    }

    dbo.Coach_Availability {
        coachav_id int PK
        id_coach int FK
        weekday tinyint
        start_time time(7)
        end_time time(7)
        valid_from date
        valid_until date
    }

    dbo.Event {
        event_id int PK
        title nvarchar(64)
        description nvarchar(256)
        start_datetime datetime2(7)
        end_datetime datetime2(7)
        image_url nvarchar(256)
        is_active bit
        created_by int FK
    }

    dbo.App_Setting {
        setting_id int PK
        setting_key nvarchar(64)
        setting_value nvarchar(128)
        updated_at date
    }

    dbo.Notification {
        notification_id int PK
        id_user int FK
        title nvarchar(128)
        message nvarchar(256)
        type tinyint
        entity_type nvarchar(32)
        entity_id int
        created_at datetime2(7)
        read_at datetime2(7)
        is_sent bit
        is_deleted bit
    }

    dbo.Item_Requisition {
        requisition_id int PK
        item_variant_id int FK
        id_parent int FK
        quantity int
        requested_at datetime2(7)
        need_from datetime2(7)
        need_until datetime2(7)
        returned_at datetime2(7)
        status tinyint
        returned_quantity int
        note nvarchar(128)
    }

    dbo.Item {
        item_id int PK
        name nvarchar(128)
        description nvarchar(256)
        from_school bit
        id_owner int FK
        id_category int FK
        created_at date
        is_active bit
        contact_address nvarchar(128)
        contact_phone nvarchar(15)
        contact_email nvarchar(254)
    }

    dbo.Item_Variant {
        variant_id int PK
        id_item int FK
        color nvarchar(32)
        size nvarchar(8)
        quantity int
        price decimal(10,2)
        is_active bit
    }

    dbo.Item_Images {
        image_id int PK
        id_item int FK
        image_url nvarchar(256)
    }

    dbo.Item_Category {
        category_id int PK
        catg_name nvarchar(128)
        is_active bit
    }

    dbo.User_Role {
        id_user int FK
        id_role tinyint FK
    }

    dbo.Role {
        role_id tinyint PK
        role_name nvarchar(32)
    }

mermaid
relationships (1, 0 , "*" = many, "/" = or, "-" = to)

    user
        User 1—0/1 Person_Info
        User 1—0/* User_Role
        User 1-0/* Coach_Class (created_by)
        User 1-0/* Notification
        User 1-0/* Item_Requisition
        User 1-0/* Item
        User 1-0/* Event
        User 0/1-0/1 Coach (coach_id is shared PK/FK)

    role
        Role 1-0/* User_Role

    student
        Student 0/*-1 User (Parent via parent_user_id)
        Student 1—0/* Participant
        Student 1—1 Person_Info

    coach
        Coach 0/*—1 User
        Coach 1-0/* Coach_Modality
        Coach 1-0/* Coach_Availability
        Coach 0/1-0/* Blocked_Period
        Coach 1-0/* Coach_Class

    studio
        Studio 1—0/* Studio_Modality
        Studio 1-0/* Coach_Class
        Studio 0/1-0/* Blocked_Period

    modality
        Modality 1-0/* Coach_Class
        Modality 1-0/* Coach_Modality
        Modality 1-0/* Studio_Modality

    item
        Item 1—1/* Item_Variant
        Item 1—1/* Item_Images
        Item 0/*-0/1 Item_Category

    item_requisition
        Item_Requisition 0/*-1 Item_Variant
        Item_Requisition 0/*-1 User (Parent via id_parent)
