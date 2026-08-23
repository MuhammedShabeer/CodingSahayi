using System;

namespace CodingSahayi.Data
{
    public class Project
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string WorkspacePath { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        
        public System.Collections.Generic.ICollection<Conversation> Conversations { get; set; } = new System.Collections.Generic.List<Conversation>();
    }

    public class Conversation
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
        
        public Project Project { get; set; } = null!;
    }

    public class ChatMessageEntity
    {
        public int Id { get; set; }
        public int ConversationId { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        
        public Conversation Conversation { get; set; } = null!;
    }

    public class ProjectKnowledge
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string TaskDescription { get; set; } = string.Empty;
        public string LearnedImplementation { get; set; } = string.Empty;
        public DateTime DateLearned { get; set; }
    }
}