using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoppAddresd.Community.Migrations
{
    /// <inheritdoc />
    public partial class RenameAntaresBrandInCommunityData : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// Renombra la marca visible en datos ya sembrados de la comunidad (seed
        /// aditivo). No toca handles (@antares*), slugs ni identificadores técnicos.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE community.profiles
                SET display_name = replace(display_name, 'ANTARES', 'Copp Adresd')
                WHERE display_name LIKE '%ANTARES%';

                UPDATE community.profiles
                SET bio = replace(bio, 'ANTARES', 'Copp Adresd')
                WHERE bio LIKE '%ANTARES%';

                UPDATE community.posts
                SET body = replace(body, 'ANTARES', 'Copp Adresd')
                WHERE body LIKE '%ANTARES%';

                UPDATE community.comments
                SET body = replace(body, 'ANTARES', 'Copp Adresd')
                WHERE body LIKE '%ANTARES%';

                UPDATE community.messages
                SET body = replace(body, 'ANTARES', 'Copp Adresd')
                WHERE body LIKE '%ANTARES%';

                UPDATE community.live_chat_messages
                SET body = replace(body, 'ANTARES', 'Copp Adresd')
                WHERE body LIKE '%ANTARES%';

                UPDATE community.feed_events
                SET body = replace(body, 'ANTARES', 'Copp Adresd')
                WHERE body LIKE '%ANTARES%';

                UPDATE community.chat_groups
                SET name = replace(name, 'ANTARES', 'Copp Adresd')
                WHERE name LIKE '%ANTARES%';

                UPDATE community.clubs
                SET name = replace(name, 'ANTARES', 'Copp Adresd')
                WHERE name LIKE '%ANTARES%';

                UPDATE community.clubs
                SET description = replace(description, 'ANTARES', 'Copp Adresd')
                WHERE description LIKE '%ANTARES%';

                UPDATE community.club_events
                SET title = replace(title, 'ANTARES', 'Copp Adresd')
                WHERE title LIKE '%ANTARES%';

                UPDATE community.club_events
                SET description = replace(description, 'ANTARES', 'Copp Adresd')
                WHERE description LIKE '%ANTARES%';

                UPDATE community.live_sessions
                SET title = replace(title, 'ANTARES', 'Copp Adresd')
                WHERE title LIKE '%ANTARES%';

                UPDATE community.network_channels
                SET name = replace(name, 'ANTARES', 'Copp Adresd')
                WHERE name LIKE '%ANTARES%';
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reversible por datos: volver al nombre de marca anterior.
            migrationBuilder.Sql(
                """
                UPDATE community.profiles
                SET display_name = replace(display_name, 'Copp Adresd', 'ANTARES')
                WHERE display_name LIKE '%Copp Adresd%';

                UPDATE community.profiles
                SET bio = replace(bio, 'Copp Adresd', 'ANTARES')
                WHERE bio LIKE '%Copp Adresd%';

                UPDATE community.posts
                SET body = replace(body, 'Copp Adresd', 'ANTARES')
                WHERE body LIKE '%Copp Adresd%';

                UPDATE community.comments
                SET body = replace(body, 'Copp Adresd', 'ANTARES')
                WHERE body LIKE '%Copp Adresd%';

                UPDATE community.messages
                SET body = replace(body, 'Copp Adresd', 'ANTARES')
                WHERE body LIKE '%Copp Adresd%';

                UPDATE community.live_chat_messages
                SET body = replace(body, 'Copp Adresd', 'ANTARES')
                WHERE body LIKE '%Copp Adresd%';

                UPDATE community.feed_events
                SET body = replace(body, 'Copp Adresd', 'ANTARES')
                WHERE body LIKE '%Copp Adresd%';

                UPDATE community.chat_groups
                SET name = replace(name, 'Copp Adresd', 'ANTARES')
                WHERE name LIKE '%Copp Adresd%';

                UPDATE community.clubs
                SET name = replace(name, 'Copp Adresd', 'ANTARES')
                WHERE name LIKE '%Copp Adresd%';

                UPDATE community.clubs
                SET description = replace(description, 'Copp Adresd', 'ANTARES')
                WHERE description LIKE '%Copp Adresd%';

                UPDATE community.club_events
                SET title = replace(title, 'Copp Adresd', 'ANTARES')
                WHERE title LIKE '%Copp Adresd%';

                UPDATE community.club_events
                SET description = replace(description, 'Copp Adresd', 'ANTARES')
                WHERE description LIKE '%Copp Adresd%';

                UPDATE community.live_sessions
                SET title = replace(title, 'Copp Adresd', 'ANTARES')
                WHERE title LIKE '%Copp Adresd%';

                UPDATE community.network_channels
                SET name = replace(name, 'Copp Adresd', 'ANTARES')
                WHERE name LIKE '%Copp Adresd%';
                """
            );
        }
    }
}
