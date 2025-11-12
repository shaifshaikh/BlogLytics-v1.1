using Microsoft.AspNetCore.Mvc;
using Registration.Models;
using Registration.Repository;
using Registration.Repository.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Registration.Controllers
{
    public class BlogController : Controller
    {
        private readonly IBlogRepository _blogRepository;
        private readonly IUserActionRepository _userActionRepository;
        private readonly ICommentRepository _commentRepository;
        private readonly IFollowRepository _followRepository;
        private readonly IReportRepository _reportRepository;
        private readonly IUserInterestRepository _userInterestRepository;
        private readonly IWebHostEnvironment _environment;

        public BlogController(
            IBlogRepository blogRepository,
            IUserActionRepository userActionRepository,
            ICommentRepository commentRepository,
            IFollowRepository followRepository,
            IReportRepository reportRepository,
            IUserInterestRepository userInterestRepository,
            IWebHostEnvironment environment)
        {
            _blogRepository = blogRepository;
            _userActionRepository = userActionRepository;
            _commentRepository = commentRepository;
            _followRepository = followRepository;
            _reportRepository = reportRepository;
            _userInterestRepository = userInterestRepository;
            _environment = environment;
        }

        // ==================== FEED ENDPOINTS ====================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetFeed(int page = 1, int pageSize = 10)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            try
            {
                var blogs = await _blogRepository.GetFeedForUserAsync(int.Parse(userId), page, pageSize);
                var totalBlogs = await _blogRepository.GetTotalBlogsCountAsync();
                var totalPages = (int)Math.Ceiling(totalBlogs / (double)pageSize);

                return Json(new
                {
                    success = true,
                    blogs = blogs,
                    currentPage = page,
                    totalPages = totalPages,
                    hasNextPage = page < totalPages
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

      

        [HttpGet]
        public async Task<IActionResult> Trending(int page = 1, int pageSize = 10)
        {
            try
            {
                var userId = HttpContext.Session.GetString("UserId");
                var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

                if (string.IsNullOrEmpty(userId))
                {
                    if (isAjax)
                    {
                        // AJAX call → return JSON for your JavaScript handler
                        return Json(new { success = false, message = "User not authenticated" });
                    }
                    else
                    {
                        // Direct browser hit → redirect to login page instead of showing JSON
                        return RedirectToAction("Login", "Account");
                    }
                }
                var blogs = await _blogRepository.GetTrendingBlogsAsync(page, pageSize);
                var totalBlogs = await _blogRepository.GetTotalBlogsCountAsync();
                var totalPages = (int)Math.Ceiling(totalBlogs / (double)pageSize);

                return Json(new
                {
                    success = true,
                    blogs = blogs,
                    currentPage = page,
                    totalPages = totalPages,
                    hasNextPage = page < totalPages
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> FilterByCategory(int categoryId, int page = 1, int pageSize = 10)
        {
            try
            {
                var blogs = await _blogRepository.GetBlogsByCategoryAsync(categoryId, page, pageSize);
                var totalBlogs = await _blogRepository.GetTotalBlogsCountAsync();
                var totalPages = (int)Math.Ceiling(totalBlogs / (double)pageSize);

                return Json(new
                {
                    success = true,
                    blogs = blogs,
                    currentPage = page,
                    totalPages = totalPages,
                    hasNextPage = page < totalPages
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                // Get blog details
                var blog = await _blogRepository.GetBlogByIdAsync(id);
                if (blog == null)
                {
                    return NotFound();
                }

                // Record view action
                await _userActionRepository.RecordActionAsync(int.Parse(userId), id, "view");
                await _blogRepository.IncrementViewCountAsync(id);
                await _userActionRepository.UpdateUserInterestScoresAsync(
                    int.Parse(userId), blog.CategoryId, "view");

                // Get comments
                var comments = await _commentRepository.GetCommentsByBlogIdAsync(id);

                // Check if user liked this blog
                var isLiked = await _userActionRepository.HasUserLikedBlogAsync(int.Parse(userId), id);

                // Check if following author
                var isFollowing = await _followRepository.IsFollowingAsync(int.Parse(userId), blog.UserId);

                var viewModel = new BlogDetailsViewModel
                {
                    Blog = blog,
                    Comments = comments,
                    IsLiked = isLiked,
                    IsFollowing = isFollowing
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading blog details: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // ==================== LIKE ENDPOINTS ====================

        [HttpPost]
        public async Task<IActionResult> Like([FromBody] int blogId)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            try
            {
                // Check if already liked
                var alreadyLiked = await _userActionRepository.HasUserLikedBlogAsync(int.Parse(userId), blogId);
                if (alreadyLiked)
                {
                    return Json(new { success = false, message = "Already liked" });
                }

                // Get blog to find category
                var blog = await _blogRepository.GetBlogByIdAsync(blogId);
                if (blog == null)
                {
                    return Json(new { success = false, message = "Blog not found" });
                }

                // Record like action
                await _userActionRepository.RecordActionAsync(int.Parse(userId), blogId, "like");
                await _blogRepository.IncrementLikeCountAsync(blogId);

                // Update user interest scores
                await _userActionRepository.UpdateUserInterestScoresAsync(
                    int.Parse(userId), blog.CategoryId, "like");

                return Json(new { success = true, message = "Blog liked successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Unlike([FromBody] int blogId)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            try
            {
                await _userActionRepository.RemoveLikeAsync(int.Parse(userId), blogId);
                await _blogRepository.DecrementLikeCountAsync(blogId);

                return Json(new { success = true, message = "Like removed successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ==================== COMMENT ENDPOINTS ====================

        [HttpPost]
        public async Task<IActionResult> AddComment([FromBody] Comment comment)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            if (string.IsNullOrWhiteSpace(comment.Content))
            {
                return Json(new { success = false, message = "Comment cannot be empty" });
            }

            try
            {
                comment.UserId = int.Parse(userId);
                comment.CreatedAt = DateTime.Now;

                // Add comment
                var commentId = await _commentRepository.AddCommentAsync(comment);

                // Record comment action
                await _userActionRepository.RecordActionAsync(int.Parse(userId), comment.BlogId, "comment");
                await _blogRepository.IncrementCommentCountAsync(comment.BlogId);

                // Get blog to find category
                var blog = await _blogRepository.GetBlogByIdAsync(comment.BlogId);

                // Update user interest scores
                await _userActionRepository.UpdateUserInterestScoresAsync(
                    int.Parse(userId), blog.CategoryId, "comment");

                // Get the newly created comment with user details
                var comments = await _commentRepository.GetCommentsByBlogIdAsync(comment.BlogId);
                var newComment = comments.FirstOrDefault(c => c.CommentId == commentId);

                return Json(new
                {
                    success = true,
                    message = "Comment added successfully",
                    comment = newComment
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteComment([FromBody] int commentId)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            try
            {
                await _commentRepository.DeleteCommentAsync(commentId);
                return Json(new { success = true, message = "Comment deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ==================== SHARE ENDPOINT ====================

        [HttpPost]
        public async Task<IActionResult> RecordShare([FromBody] int blogId)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            try
            {
                // Get blog to find category
                var blog = await _blogRepository.GetBlogByIdAsync(blogId);
                if (blog == null)
                {
                    return Json(new { success = false, message = "Blog not found" });
                }

                // Record share action
                await _userActionRepository.RecordActionAsync(int.Parse(userId), blogId, "share");
                await _blogRepository.IncrementShareCountAsync(blogId);

                // Update user interest
                // Update user interest scores
                await _userActionRepository.UpdateUserInterestScoresAsync(
                    int.Parse(userId), blog.CategoryId, "share");

                return Json(new { success = true, message = "Share recorded successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ==================== FOLLOW ENDPOINTS ====================

        [HttpPost]
        public async Task<IActionResult> FollowUser([FromBody] int authorUserId)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            if (int.Parse(userId) == authorUserId)
            {
                return Json(new { success = false, message = "You cannot follow yourself" });
            }

            try
            {
                await _followRepository.FollowUserAsync(int.Parse(userId), authorUserId);
                return Json(new { success = true, message = "User followed successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UnfollowUser([FromBody] int authorUserId)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            try
            {
                await _followRepository.UnfollowUserAsync(int.Parse(userId), authorUserId);
                return Json(new { success = true, message = "User unfollowed successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ==================== REPORT ENDPOINT ====================

        [HttpPost]
        public async Task<IActionResult> ReportContent([FromBody] ReportViewModel reportModel)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            if (string.IsNullOrEmpty(reportModel.Reason))
            {
                return Json(new { success = false, message = "Please select a reason" });
            }

            try
            {
                var report = new Report
                {
                    ReporterId = int.Parse(userId),
                    BlogId = reportModel.BlogId,
                    CommentId = reportModel.CommentId,
                    ReportedUserId = reportModel.ReportedUserId,
                    Reason = reportModel.Reason,
                    Description = reportModel.Description,
                    Status = "pending",
                    CreatedAt = DateTime.Now
                };

                await _reportRepository.SubmitReportAsync(report);

                return Json(new
                {
                    success = true,
                    message = "Report submitted successfully. We'll review it soon."
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ==================== CREATE BLOG ENDPOINTS ====================

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            // Get all categories for dropdown
            var categories = await _userInterestRepository.GetAllActiveCategoriesAsync();
            ViewBag.Categories = categories;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] Blog blog, IFormFile imageFile)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            if (string.IsNullOrWhiteSpace(blog.Title) || string.IsNullOrWhiteSpace(blog.Content))
            {
                return Json(new { success = false, message = "Title and content are required" });
            }

            try
            {
                blog.UserId = int.Parse(userId);
                blog.CreatedAt = DateTime.Now;
                blog.UpdatedAt = DateTime.Now;

                // Handle image upload
                if (imageFile != null && imageFile.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "blogs");
                    Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(fileStream);
                    }

                    blog.ImageUrl = "/uploads/blogs/" + uniqueFileName;
                }

                var blogId = await _blogRepository.CreateBlogAsync(blog);

                return Json(new
                {
                    success = true,
                    message = "Blog created successfully",
                    blogId = blogId
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ==================== MY BLOGS ENDPOINTS ====================

        [HttpGet]
        public async Task<IActionResult> MyBlogs()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var blogs = await _blogRepository.GetBlogsByUserAsync(int.Parse(userId));
                return View(blogs);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading your blogs: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // ==================== EDIT BLOG ENDPOINTS ====================

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var blog = await _blogRepository.GetBlogByIdAsync(id);
                if (blog == null)
                {
                    return NotFound();
                }

                // Check if user owns this blog
                if (blog.UserId != int.Parse(userId))
                {
                    return Forbid();
                }

                var categories = await _userInterestRepository.GetAllActiveCategoriesAsync();
                ViewBag.Categories = categories;

                return View(blog);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error loading blog: " + ex.Message;
                return RedirectToAction("MyBlogs");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Edit([FromForm] Blog blog, IFormFile imageFile)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            try
            {
                var existingBlog = await _blogRepository.GetBlogByIdAsync(blog.BlogId);
                if (existingBlog == null)
                {
                    return Json(new { success = false, message = "Blog not found" });
                }

                // Check if user owns this blog
                if (existingBlog.UserId != int.Parse(userId))
                {
                    return Json(new { success = false, message = "Unauthorized" });
                }

                // Handle new image upload
                if (imageFile != null && imageFile.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "blogs");
                    Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(fileStream);
                    }

                    blog.ImageUrl = "/uploads/blogs/" + uniqueFileName;
                }
                else
                {
                    blog.ImageUrl = existingBlog.ImageUrl; // Keep existing image
                }

                blog.UpdatedAt = DateTime.Now;
                await _blogRepository.UpdateBlogAsync(blog);

                return Json(new { success = true, message = "Blog updated successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ==================== DELETE BLOG ENDPOINT ====================

        [HttpPost]
        public async Task<IActionResult> Delete([FromBody] int blogId)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            try
            {
                var blog = await _blogRepository.GetBlogByIdAsync(blogId);
                if (blog == null)
                {
                    return Json(new { success = false, message = "Blog not found" });
                }

                // Check if user owns this blog
                if (blog.UserId != int.Parse(userId))
                {
                    return Json(new { success = false, message = "Unauthorized" });
                }

                await _blogRepository.DeleteBlogAsync(blogId);

                return Json(new { success = true, message = "Blog deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}